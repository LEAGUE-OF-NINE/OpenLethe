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
public class MirrorDungeonEventsHandlerTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new { userAuth = new { authCode = jwt }, parameters = p };

    private static async Task<(string jwt, string name)> NewAccount(DbWebAppFactory f)
    {
        var name = $"mdevt_{Guid.NewGuid():N}";
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

    // Real static-data: abnormality-event id 800201, actionEvent.eachOptionList[1].resultList[0].nextEventID = 800202.
    [SkippableFact]
    public async Task UpdateMapNode_NodeEidPath_ResolvesEventViaDungeonMapNode()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        save.dungeonMap.ns.Add(new Ns { f = 0, s = 1, nid = 101, e = 1, eid = 800201 });
        save.currentInfo.cn.nid = 101;
        save.currentInfo.pce.Clear();
        save.currentInfo.dul.Add(new Dungeonunitlist1 { pid = 7, ch = 10000, cm = 0 });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/UpdateMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 1, nid = 101 },
            choiceEventData = new { sl = new long[] { 1 }, cs = 0, ri = 0 },
            dungeonUnitList = Array.Empty<object>(),
            updatedEgoGifts = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var result = doc.RootElement.GetProperty("result");
            Assert.Empty(result.GetProperty("prevChoiceEvent").EnumerateArray());
            Assert.True(result.TryGetProperty("currentEgoGifts", out _));
            Assert.True(result.TryGetProperty("dungeonUnitList", out _));
        }

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(800202, stored.currentInfo.pce[0].nei);
        Assert.Equal(-1, stored.currentInfo.pce[0].cs);
    }

    // Real static-data: abnormality-event id 805001, actionEvent.eachOptionList[0].resultList[0].nextEventID = 805002.
    [SkippableFact]
    public async Task UpdateMapNode_PceNeiPath_ResolvesEventViaPreviousChoice()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        // No matching dungeonMap node - only the pce.nei path can resolve the event id.
        save.currentInfo.cn.nid = 999;
        save.currentInfo.pce = new() { new ChoiceEventData { sl = new() { 0 }, cs = -1, ri = 0, nei = 805001 } };
        save.currentInfo.dul.Add(new Dungeonunitlist1 { pid = 7, ch = 10000, cm = 0 });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/UpdateMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 0, nid = 999 },
            choiceEventData = new { sl = new long[] { 0 }, cs = 0, ri = 0 },
            dungeonUnitList = Array.Empty<object>(),
            updatedEgoGifts = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        // Newly-inserted entry at pce[0] carries the processed next id for event 805001.
        Assert.Equal(805002, stored.currentInfo.pce[0].nei);
    }

    [SkippableFact]
    public async Task UpdateMapNode_NoSave_Returns500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccount(f);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/UpdateMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 0, nid = 1 },
            choiceEventData = new { sl = new long[] { 0 }, cs = 0, ri = 0 },
            dungeonUnitList = Array.Empty<object>(),
            updatedEgoGifts = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [SkippableFact]
    public async Task UpdateMapNode_EidUnresolvable_Returns500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        save.currentInfo.pce.Clear();
        save.currentInfo.cn.nid = 999999; // matches no node in dungeonMap.ns
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/UpdateMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 0, nid = 999999 },
            choiceEventData = new { sl = new long[] { 0 }, cs = 0, ri = 0 },
            dungeonUnitList = Array.Empty<object>(),
            updatedEgoGifts = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }
}
