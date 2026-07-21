using System.Linq;
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
public class MirrorDungeonMapHandlerTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new { userAuth = new { authCode = jwt }, parameters = p };

    private static async Task<(string jwt, string name)> NewAccount(DbWebAppFactory f)
    {
        var name = $"mdmap_{Guid.NewGuid():N}";
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
    public async Task FullFlow_AcquireStart_SelectThemeFloor_EnterMapNode()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        // 1. AcquireStart: egs grows by selectedEgoGiftIds, tfps gets populated.
        var acquireResp = await client.PostAsJsonAsync("/api/AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon",
            Body(jwt, new { selectedEgoGiftIds = new long[] { 9001, 9002 } }));
        Assert.Equal(HttpStatusCode.OK, acquireResp.StatusCode);
        using (var doc = JsonDocument.Parse(await acquireResp.Content.ReadAsStringAsync()))
        {
            var save = doc.RootElement.GetProperty("result").GetProperty("saveInfo");
            var egs = save.GetProperty("currentInfo").GetProperty("egs");
            Assert.Equal(2, egs.GetArrayLength());
            Assert.Equal(9001, egs[0].GetProperty("id").GetInt64());
            Assert.Equal(9002, egs[1].GetProperty("id").GetInt64());
            Assert.True(save.GetProperty("currentInfo").GetProperty("tfps").GetArrayLength() > 0);
        }

        // 2. SelectThemeFloor(1001) at floor 0: generates the map, records the theme, sets startKeyword.
        var selectResp = await client.PostAsJsonAsync("/api/SelectThemeFloorMirrorDungeon",
            Body(jwt, new { selectedIdx = 0, selectedThemeFoorId = 1001 }));
        Assert.Equal(HttpStatusCode.OK, selectResp.StatusCode);
        using (var doc = JsonDocument.Parse(await selectResp.Content.ReadAsStringAsync()))
        {
            var save = doc.RootElement.GetProperty("result").GetProperty("saveInfo");
            Assert.True(save.GetProperty("dungeonMap").GetProperty("ns").GetArrayLength() > 0);
            var tfs = save.GetProperty("currentInfo").GetProperty("tfs");
            Assert.True(tfs.GetArrayLength() > 0);
            Assert.Equal(1001, tfs[tfs.GetArrayLength() - 1].GetProperty("tfid").GetInt64());
            Assert.Equal("Penetrate", save.GetProperty("currentInfo").GetProperty("startKeyword").GetString());
        }

        // 3. Force the generated shop node to "super shop" (eid != 0) so shop_gift_count is
        // deterministic (8), then enter it.
        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        var shopNode = stored.dungeonMap.ns.Single(n => n.e == 10);
        shopNode.eid = 1;
        await SetSave(f, name, stored);

        var enterResp = await client.PostAsJsonAsync("/api/EnterMirrorDungeonMapNode",
            Body(jwt, new { currentnode = new { f = shopNode.f, s = shopNode.s, nid = shopNode.nid } }));
        Assert.Equal(HttpStatusCode.OK, enterResp.StatusCode);
        using (var doc = JsonDocument.Parse(await enterResp.Content.ReadAsStringAsync()))
        {
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(shopNode.nid, result.GetProperty("currentNode").GetProperty("nid").GetInt64());
            Assert.Equal(8, result.GetProperty("shopInfo").GetProperty("egpool").GetArrayLength());
        }

        var final = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(8, final.currentInfo.shop.egpool.Count);
        Assert.Equal(shopNode.nid, final.currentInfo.cn.nid);
    }

    [SkippableFact]
    public async Task ExitMapNode_BossNode_RaisesCostAndSetsEnemyBufReward()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        save.dungeonMap.ns.Add(new Ns { f = 0, s = 5, nid = 501, e = 6, eid = 2060122 });
        save.currentInfo.tfs.Add(new Tfs { f = 0, tid = 0, idx = 0, tfid = 1001, egs = new() { 9019, 9017 } });
        save.currentInfo.dul.Add(new Dungeonunitlist1 { pid = 7, ch = 5000, cm = 0, l = 60 });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/ExitMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 5, nid = 501 },
            dungeonunitlist = new[] { new { pid = 7, ch = 5000, cm = 0, l = 60, upidx = Array.Empty<long>(), es = Array.Empty<object>() } },
            noderesult = 1,
            choiceEventData = new { },
            isupdatedEgoSkillStock = 0,
            egoSkillStockList = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var currentInfo = doc.RootElement.GetProperty("result").GetProperty("currentInfo");
        Assert.Equal(500 + 200, currentInfo.GetProperty("cost").GetInt64()); // floor 0 boss cost = 200
        var rre = currentInfo.GetProperty("rre");
        Assert.Single(rre.EnumerateArray());
        Assert.Equal("GetEgogiftWithEnemyBuf", rre[0].GetProperty("rt").GetString());
        Assert.Equal(2, rre[0].GetProperty("pool").GetArrayLength()); // egs from tfs.last
        Assert.Equal(4, rre[0].GetProperty("pool_v3").GetArrayLength()); // 4 levelup rolls
        foreach (var unit in currentInfo.GetProperty("dul").EnumerateArray())
            Assert.Equal(10000, unit.GetProperty("ch").GetInt64());

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(700, stored.currentInfo.cost);
        Assert.All(stored.currentInfo.dul, u => Assert.Equal(10000, u.ch));
    }

    [SkippableFact]
    public async Task ExitMapNode_AbnoBattleNode_SetsGetEgogiftReward()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        save.dungeonMap.ns.Add(new Ns { f = 0, s = 1, nid = 101, e = 5, eid = 2060116 });
        save.currentInfo.tfs.Add(new Tfs { f = 0, tid = 0, idx = 0, tfid = 1001, egs = new() });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/ExitMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 1, nid = 101 },
            dungeonunitlist = Array.Empty<object>(),
            noderesult = 1,
            choiceEventData = new { },
            isupdatedEgoSkillStock = 0,
            egoSkillStockList = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var rre = doc.RootElement.GetProperty("result").GetProperty("currentInfo").GetProperty("rre");
        Assert.Single(rre.EnumerateArray());
        Assert.Equal("GetEgogift", rre[0].GetProperty("rt").GetString());

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(500 + 100, stored.currentInfo.cost); // node e=5 default cost 100
    }

    [SkippableFact]
    public async Task ExitMapNode_UnknownNodeId_Returns400()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var resp = await client.PostAsJsonAsync("/api/ExitMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 0, nid = 999999 },
            dungeonunitlist = Array.Empty<object>(),
            noderesult = 1,
            choiceEventData = new { },
            isupdatedEgoSkillStock = 0,
            egoSkillStockList = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [SkippableFact]
    public async Task RecreateThemeFloorPoolMirrorDungeon_ReturnsFreshTfps()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));
        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/RecreateThemeFloorPoolMirrorDungeon", Body(jwt, new { }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var tfps = doc.RootElement.GetProperty("result").GetProperty("saveInfo").GetProperty("currentInfo").GetProperty("tfps");
        Assert.True(tfps.GetArrayLength() > 0);

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.True(stored.currentInfo.tfps.Count > 0);
        Assert.Empty(stored.currentInfo.seps);
    }

    [SkippableFact]
    public async Task SelectThemeFloorMirrorDungeon_UnknownThemeId_Returns500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var resp = await client.PostAsJsonAsync("/api/SelectThemeFloorMirrorDungeon",
            Body(jwt, new { selectedIdx = 0, selectedThemeFoorId = 999999999 }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [SkippableFact]
    public async Task AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon_NoSave_Returns500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccount(f);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon",
            Body(jwt, new { selectedEgoGiftIds = Array.Empty<long>() }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }
}
