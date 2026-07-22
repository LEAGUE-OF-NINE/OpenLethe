using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server;
using OpenLethe.Server.Auth;
using OpenLethe.Server.Wire;
using Xunit;

[Collection("postgres")]
public class MirrorDungeonRewardsHandlerTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new { userAuth = new { authCode = jwt }, parameters = p };

    private static async Task<(string jwt, string name)> NewAccount(DbWebAppFactory f)
    {
        var name = $"mdrwd_{Guid.NewGuid():N}";
        using var scope = f.Services.CreateScope();
        await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).GetOrCreateByUsernameAsync(name);
        var jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        return (jwt, name);
    }

    private static async Task<Account> GetAccount(DbWebAppFactory f, string name)
    {
        using var scope = f.Services.CreateScope();
        return (await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name))!;
    }

    private static async Task SetSave(DbWebAppFactory f, string name, MirrorOriginSaveInfo save)
    {
        using var scope = f.Services.CreateScope();
        var db2 = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var acc = (await new AccountStore(db2).FindByUsernameAsync(name))!;
        acc.MdSaveInfo = AccountFields.Set(save);
        await db2.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task CombineEgoGift_RemovesMaterialsAndAddsResult()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        save.currentInfo.egs.Add(new AcquiredEgogifts { id = 9003 });
        save.currentInfo.egs.Add(new AcquiredEgogifts { id = 9053 });
        save.currentInfo.egs.Add(new AcquiredEgogifts { id = 9157 });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/CombineEgoGiftMirrorDungeon",
            Body(jwt, new { materialEgoGiftIds = new[] { 9003, 9053, 9157 }, keyword = "Sinking", isOrigin = 0 }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        // 3 materials removed, 1 fused result pushed. 9003/9053/9157 is a fixed recipe -> 9088.
        Assert.Single(stored.currentInfo.egs);
        Assert.Equal(9088, stored.currentInfo.egs[0].id);
    }

    [SkippableFact]
    public async Task CombineEgoGift_NoAuth_Returns401()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var client = f.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/CombineEgoGiftMirrorDungeon",
            Body("not-a-real-auth-code", new { materialEgoGiftIds = new long[0], keyword = "", isOrigin = 0 }));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [SkippableFact]
    public async Task RefreshShopEgoGifts_DeductsFifteenCostAndRefillsPool()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        save.currentInfo.cost = 100;
        save.currentInfo.leveladders.Add(1); // floor 1
        save.currentInfo.shop.peg.Add(9001);
        // make IsSuperShop resolve: current node with e == 10
        save.currentInfo.cn.nid = 5;
        save.dungeonMap.ns.Add(new Ns { nid = 5, e = 10, eid = 0 });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/RefreshShopEgoGiftsMirrorDungeon",
            Body(jwt, new { keyword = "Sinking", isOrigin = 0 }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(85, stored.currentInfo.cost);
        // shop_gift_count for a non-super shop is 5, minus 1 already-bought = 4 new + the 1 peg.
        Assert.Equal(9001, stored.currentInfo.shop.egpool[0]);
        Assert.InRange(stored.currentInfo.shop.egpool.Count, 1, 5);
    }

    [SkippableFact]
    public async Task GetEgoGiftRecord_ReturnsAllEgoGiftsAndThemes()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccount(f);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/GetMirrorDungeonEgoGiftRecord", Body(jwt, new { }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.NotEmpty(result.GetProperty("acquiredegogifts").EnumerateArray());
        Assert.NotEmpty(result.GetProperty("themeFloorIds").EnumerateArray());
    }

    [SkippableFact]
    public async Task ExitMirrorDungeon_ReturnsClearedDungeon()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccount(f);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/ExitMirrorDungeon", Body(jwt, new { }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(1, result.GetProperty("isEndDungeon").GetInt64());
        Assert.Equal(1, result.GetProperty("isclear").GetInt64());
        Assert.Empty(result.GetProperty("statistics").EnumerateArray());
    }
}
