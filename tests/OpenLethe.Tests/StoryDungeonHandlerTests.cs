using System.Collections.Generic;
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
public class StoryDungeonHandlerTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new { userAuth = new { authCode = jwt }, parameters = p };

    private static async Task<(string jwt, string name)> NewAccount(DbWebAppFactory f)
    {
        var name = $"sd_{Guid.NewGuid():N}";
        using var scope = f.Services.CreateScope();
        await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).GetOrCreateByUsernameAsync(name);
        var jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        return (jwt, name);
    }

    [SkippableFact]
    public async Task EnterStoryDungeon_LoadsMap_PersistsStartNode()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/EnterStoryDungeon",
            Body(jwt, new { stageid = 10501, personalities = new object[0] }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var save = doc.RootElement.GetProperty("result").GetProperty("saveInfo");
        Assert.Equal(10501, save.GetProperty("dungeonid").GetInt64());
        Assert.Equal(10501, save.GetProperty("currentinfo").GetProperty("cn").GetProperty("nid").GetInt64());

        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var stored = AccountFields.Get<StorySaveInfo>(acc!.StorySaveInfo)!;
        Assert.Equal(10501, stored.currentinfo.cn.nid);
    }

    [SkippableFact]
    public async Task EnterStoryDungeon_UnknownMap_500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccount(f);
        var client = f.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/EnterStoryDungeon", Body(jwt, new { stageid = 999999, personalities = new object[0] }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [SkippableFact]
    public async Task ReEnterStoryDungeon_EchoesStoredSave()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterStoryDungeon", Body(jwt, new { stageid = 10501, personalities = new object[0] }));

        var resp = await client.PostAsJsonAsync("/api/ReEnterStoryDungeon", Body(jwt, new { }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(10501, result.GetProperty("saveInfo").GetProperty("dungeonid").GetInt64());
        Assert.Equal(0, result.GetProperty("isAllDie").GetInt64());
    }

    [SkippableFact]
    public async Task EnterStoryDungeonMapNode_UpdatesCurrentNode()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterStoryDungeon", Body(jwt, new { stageid = 10501, personalities = new object[0] }));

        var resp = await client.PostAsJsonAsync("/api/EnterStoryDungeonMapNode",
            Body(jwt, new { floornumber = 1, sectornumber = 2, nodeid = 10505 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(10505, result.GetProperty("node").GetProperty("nid").GetInt64());
        Assert.Equal(3, result.GetProperty("nr").GetInt64());

        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var stored = AccountFields.Get<StorySaveInfo>(acc!.StorySaveInfo)!;
        Assert.Contains(10505L, stored.currentinfo.pnids);
        Assert.Equal(1, stored.currentinfo.nr);
    }

    private static async Task SeedSave(DbWebAppFactory f, string name, StorySaveInfo save)
    {
        using var scope = f.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var acc = await new AccountStore(ctx).FindByUsernameAsync(name);
        acc!.StorySaveInfo = AccountFields.Set(save);
        await ctx.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task ExitStoryDungeonMapNode_Chapter3_RemovesTorchAndPowerUp_ClearsPce()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        // chapter-3 dungeon (10301); seed egs containing torch(991008) + powerup(1032) + keeper(5)
        await SeedSave(f, name, new StorySaveInfo
        {
            dungeonid = 10301,
            currentinfo = new Currentinfo
            {
                cn = new Currentnode { f = 0, s = 0, nid = 0 },
                egs = new() { new AcquiredEgogifts { id = 991008 }, new AcquiredEgogifts { id = 1032 }, new AcquiredEgogifts { id = 5 } },
                pce = new() { new ChoiceEventData { cs = 1 } },
            },
        });

        var resp = await client.PostAsJsonAsync("/api/ExitStoryDungeonMapNode",
            Body(jwt, new { updatedEgoGifts = new object[0], dungeonunitlist = new object[0], egoSkillStockList = new object[0] }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var stored = AccountFields.Get<StorySaveInfo>(acc!.StorySaveInfo)!;
        Assert.DoesNotContain(stored.currentinfo.egs, e => e.id == 991008 || e.id == 1032);
        Assert.Contains(stored.currentinfo.egs, e => e.id == 5);
        Assert.Empty(stored.currentinfo.pce);
    }

    [SkippableFact]
    public async Task ReturnSavePointStoryDungeonMap_RewindsToSaveNode()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        // map 10501 has a save-encounter node id 10506; seed pnids ending in it
        await SeedSave(f, name, new StorySaveInfo
        {
            dungeonid = 10501,
            currentinfo = new Currentinfo
            {
                cn = new Currentnode { f = 3, s = 3, nid = 10510 },
                pnids = new() { 10501, 10506 },
            },
        });

        var resp = await client.PostAsJsonAsync("/api/ReturnSavePointStoryDungeonMap", Body(jwt, new { }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var cn = doc.RootElement.GetProperty("result").GetProperty("currentInfo").GetProperty("cn");
        Assert.Equal(10506, cn.GetProperty("nid").GetInt64());
    }

    [SkippableFact]
    public async Task ExitStoryDungeon_RegistersWonNode()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var name = $"sd_{Guid.NewGuid():N}";
        string jwt;
        using (var scope = f.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var acc = await new AccountStore(ctx).GetOrCreateByUsernameAsync(name);
            acc.ChapterState = AccountFields.Set(new List<MainChapterState>
            {
                new() { id = 1, subcss = new() { new Subcss { id = 1, nss = new() { new Nss { id = 500 } }, rss = new() } } },
            });
            await ctx.SaveChangesAsync();
            jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        }
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/ExitStoryDungeon", Body(jwt, new { nodeid = 500 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var node = doc.RootElement.GetProperty("updated").GetProperty("mainChapterStateList")[0]
            .GetProperty("subcss")[0].GetProperty("nss")[0];
        Assert.Equal(2, node.GetProperty("ct").GetInt64());
    }

    // Real static-data: abnormality-event id 805001, actionEvent.eachOptionList[0].resultList[0].nextEventID
    // = 805002. No eventResultDataList (no reward/RNG effects), so the outcome is fully deterministic.
    [SkippableFact]
    public async Task UpdateStoryDungeonMapNode_UsesPceNeiAndPrependsChoiceEvent()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        await SeedSave(f, name, new StorySaveInfo
        {
            dungeonid = 10501,
            currentinfo = new Currentinfo
            {
                cn = new Currentnode { f = 0, s = 0, nid = 10501 },
                pce = new() { new ChoiceEventData { sl = new() { 0 }, cs = -1, ri = 0, nei = 805001 } },
            },
        });

        var resp = await client.PostAsJsonAsync("/api/UpdateStoryDungeonMapNode", Body(jwt, new
        {
            choiceEventData = new { sl = new long[] { 0 }, cs = 0, ri = 0 },
            dungeonUnitList = Array.Empty<object>(),
            updatedEgoGifts = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var result = doc.RootElement.GetProperty("result");
            Assert.Empty(result.GetProperty("prevChoiceEvent").EnumerateArray());
            Assert.True(result.TryGetProperty("currentEgoGifts", out _));
        }

        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var stored = AccountFields.Get<StorySaveInfo>(acc!.StorySaveInfo)!;
        Assert.Equal(2, stored.currentinfo.pce.Count);
        Assert.Equal(new List<long> { 0 }, stored.currentinfo.pce[0].sl);
        Assert.Equal(-1, stored.currentinfo.pce[0].cs);
        Assert.Equal(0, stored.currentinfo.pce[0].ri);
        Assert.Equal(805002, stored.currentinfo.pce[0].nei);
        // Previous entry pushed down to index 1, untouched.
        Assert.Equal(805001, stored.currentinfo.pce[1].nei);
    }

    [SkippableFact]
    public async Task UpdateStoryDungeonMapNode_EmptyChoiceList_Returns500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        await SeedSave(f, name, new StorySaveInfo
        {
            dungeonid = 10501,
            currentinfo = new Currentinfo { cn = new Currentnode { f = 0, s = 0, nid = 10501 } },
        });

        var resp = await client.PostAsJsonAsync("/api/UpdateStoryDungeonMapNode", Body(jwt, new
        {
            choiceEventData = new { sl = Array.Empty<long>(), cs = 0, ri = 0 },
            dungeonUnitList = Array.Empty<object>(),
            updatedEgoGifts = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [SkippableFact]
    public async Task UpdateStoryDungeonMapNode_NoAuth_Returns401()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/UpdateStoryDungeonMapNode", Body("not-a-real-jwt", new
        {
            choiceEventData = new { sl = new long[] { 0 }, cs = 0, ri = 0 },
            dungeonUnitList = Array.Empty<object>(),
            updatedEgoGifts = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [SkippableFact]
    public async Task UpdateStoryDungeonMapNode_IgnoresRequestUnitListAndEgoGifts()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        await SeedSave(f, name, new StorySaveInfo
        {
            dungeonid = 10501,
            currentinfo = new Currentinfo
            {
                cn = new Currentnode { f = 0, s = 0, nid = 10501 },
                pce = new() { new ChoiceEventData { sl = new() { 0 }, cs = -1, ri = 0, nei = 805001 } },
                // Seeded unit with a distinct pid/ch/cm from anything the request sends,
                // so a handler that starts consuming the request's unit list (replacing
                // or appending) is caught even if it lands on a different pid than 999.
                dul = new() { new Dungeonunitlist { pid = 42, ch = 111, cm = 222 } },
            },
        });

        var resp = await client.PostAsJsonAsync("/api/UpdateStoryDungeonMapNode", Body(jwt, new
        {
            choiceEventData = new { sl = new long[] { 0 }, cs = 0, ri = 0 },
            dungeonUnitList = new[] { new { sp = 1, gi = 2, pid = 999, ch = 3, cm = 4, mhos = 5, g = 6, l = 7, es = Array.Empty<object>(), isp = 8 } },
            updatedEgoGifts = new[] { new { id = 424242, pids = Array.Empty<long>(), un = 0, ul = 0 } },
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var stored = AccountFields.Get<StorySaveInfo>(acc!.StorySaveInfo)!;
        Assert.DoesNotContain(stored.currentinfo.dul, u => u.pid == 999);
        var seeded = Assert.Single(stored.currentinfo.dul);
        Assert.Equal(42, seeded.pid);
        Assert.Equal(111, seeded.ch);
        Assert.Equal(222, seeded.cm);
        Assert.DoesNotContain(stored.currentinfo.egs, e => e.id == 424242);
    }

    // Real static-data: abnormality-event id 101001, actionEvent.eachOptionList[0]. A
    // single Prob_1 result with eventResultDataList = [GetConfirmedEgogift(1001),
    // LoseMpOnly_10 target EveryAlly]. UpdateActionEventRewards applies every entry in
    // resultList/eventResultDataList unconditionally (no probability roll in this port),
    // and "LoseMpOnly" has no random-target case (that's only RandomAlly, not used here),
    // so the -10 sp delta to the seeded unit is fully deterministic. nextEventID is -1.
    [SkippableFact]
    public async Task UpdateStoryDungeonMapNode_AppliesDeterministicSpLossToDul()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        await SeedSave(f, name, new StorySaveInfo
        {
            dungeonid = 10501,
            currentinfo = new Currentinfo
            {
                cn = new Currentnode { f = 0, s = 0, nid = 10501 },
                pce = new() { new ChoiceEventData { sl = new() { 0 }, cs = -1, ri = 0, nei = 101001 } },
                dul = new() { new Dungeonunitlist { pid = 42, ch = 100, cm = 50 } },
            },
        });

        var resp = await client.PostAsJsonAsync("/api/UpdateStoryDungeonMapNode", Body(jwt, new
        {
            choiceEventData = new { sl = new long[] { 0 }, cs = 0, ri = 0 },
            dungeonUnitList = Array.Empty<object>(),
            updatedEgoGifts = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var stored = AccountFields.Get<StorySaveInfo>(acc!.StorySaveInfo)!;
        var unit = Assert.Single(stored.currentinfo.dul);
        Assert.Equal(42, unit.pid);
        Assert.Equal(100, unit.ch); // unaffected - effect only touches sp/cm
        Assert.Equal(40, unit.cm); // 50 - 10
        Assert.Contains(stored.currentinfo.egs, e => e.id == 1001);
        Assert.Equal(-1, stored.currentinfo.pce[0].nei);
    }

    // Finding 3: choiceEventData.sl[0] is client-controlled and cast to int for
    // MdEventManager.ProcessEvent. A naked (int) cast WRAPS on overflow, so a hostile
    // value like 4294967296 (2^32) would wrap to 0 - a valid option index. Send it
    // alongside the same seed/event as the sl:[0] test above and confirm the outcome
    // differs (no sp loss applied, no ego gift granted): the guard must reject it as
    // out-of-range rather than silently treating it as choice 0.
    [SkippableFact]
    public async Task UpdateStoryDungeonMapNode_OverflowingChoiceIndex_DoesNotWrapToZero()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        await SeedSave(f, name, new StorySaveInfo
        {
            dungeonid = 10501,
            currentinfo = new Currentinfo
            {
                cn = new Currentnode { f = 0, s = 0, nid = 10501 },
                pce = new() { new ChoiceEventData { sl = new() { 0 }, cs = -1, ri = 0, nei = 101001 } },
                dul = new() { new Dungeonunitlist { pid = 42, ch = 100, cm = 50 } },
            },
        });

        var resp = await client.PostAsJsonAsync("/api/UpdateStoryDungeonMapNode", Body(jwt, new
        {
            choiceEventData = new { sl = new long[] { 4294967296L }, cs = 0, ri = 0 },
            dungeonUnitList = Array.Empty<object>(),
            updatedEgoGifts = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var stored = AccountFields.Get<StorySaveInfo>(acc!.StorySaveInfo)!;
        var unit = Assert.Single(stored.currentinfo.dul);
        // If the cast had wrapped to 0, cm would be 40 and egs would contain 1001 -
        // the same outcome as the sl:[0] test above. Neither happened: rejected as OOB.
        Assert.Equal(50, unit.cm);
        Assert.Equal(100, unit.ch);
        Assert.DoesNotContain(stored.currentinfo.egs, e => e.id == 1001);
        Assert.Equal(-1, stored.currentinfo.pce[0].nei);
    }
}
