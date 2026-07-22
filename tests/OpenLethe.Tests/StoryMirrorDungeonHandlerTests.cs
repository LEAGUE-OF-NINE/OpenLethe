using System;
using System.Collections.Generic;
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
public class StoryMirrorDungeonHandlerTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new { userAuth = new { authCode = jwt }, parameters = p };

    private static async Task<(string jwt, string name)> NewAccount(DbWebAppFactory f)
    {
        var name = $"smd_{Guid.NewGuid():N}";
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

    private static async Task SetStoryMdSave(DbWebAppFactory f, string name, StoryMirrorSaveInfo save)
    {
        using var scope = f.Services.CreateScope();
        var db2 = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var acc = (await new AccountStore(db2).FindByUsernameAsync(name))!;
        acc.StoryMdSaveInfo = AccountFields.Set(save);
        await db2.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task EnterStoryMirrorDungeon_SeedsFreshSave()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/EnterStoryMirrorDungeon", Body(jwt, new { dungeonid = 910301, idx = 0 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(910301, stored.dungeonid);
        Assert.Equal(-1, stored.currentinfo.eid);
        Assert.Equal(200, stored.currentinfo.cost);
        Assert.Equal(1, stored.currentinfo.sepsCreated);

        Assert.Equal(7, stored.currentinfo.seps.Count);
        Assert.Equal(
            new[] { "Combustion", "Laceration", "Vibration", "Burst", "Sinking", "Breath", "Charge" },
            stored.currentinfo.seps.Select(s => s.keyword).ToArray());

        Assert.Equal(7, stored.currentinfo.ess.Count);
        Assert.Equal(new[] { "CR", "SC", "AM", "SH", "AZ", "IN", "VI" }, stored.currentinfo.ess.Select(e => e.t).ToArray());
        Assert.All(stored.currentinfo.ess, e => Assert.Equal(0, e.n));
    }

    [SkippableFact]
    public async Task EnterStoryMirrorDungeon_NoAuth_Returns401()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/EnterStoryMirrorDungeon",
            Body("not-a-real-auth-code", new { dungeonid = 910301, idx = 0 }));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [SkippableFact]
    public async Task UpdateStoryMirrorDungeon_MapsFormationAndClearsSeps()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.seps.Add(new StartEgoGiftPoolSets { setId = 0, keyword = "Combustion", pool = new() { 9001 } });
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/UpdateStoryMirrorDungeon", Body(jwt, new
        {
            formation = new[]
            {
                new
                {
                    pervPersonalityId = 0,
                    nextPersonalityId = 10101,
                    egos = new[] { new { prevEgoId = 0, nextEgoId = 500 } },
                },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Empty(stored.currentinfo.seps);
        var unit = Assert.Single(stored.currentinfo.dul);
        Assert.Equal(50, unit.sp);
        Assert.Equal(10101, unit.pid);
        Assert.Equal(10000, unit.ch);
        Assert.Equal(60, unit.l);
        var ego = Assert.Single(unit.es);
        Assert.Equal(500, ego.id);
        Assert.Equal(0, ego.g);
        Assert.Equal(0, ego.idx);
    }

    [SkippableFact]
    public async Task AcquireStartEgoGifts_GeneratesFloorAndAppendsGifts()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        await SetStoryMdSave(f, name, new StoryMirrorSaveInfo { dungeonid = 910301 });

        var resp = await client.PostAsJsonAsync("/api/AcquireStartEgoGiftsStoryMirrorDungeon", Body(jwt, new
        {
            selectedSetId = 0,
            selectedEgoGiftIds = new long[] { 9001, 9009 },
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(2, result.GetProperty("startEgoGiftCreatedCount").GetInt64());
            Assert.Empty(result.GetProperty("startEgoGiftPoolSets").EnumerateArray());
        }

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.NotEmpty(stored.map.ns);
        Assert.Contains(stored.currentinfo.egs, e => e.id == 9001);
        Assert.Contains(stored.currentinfo.egs, e => e.id == 9009);
    }

    [SkippableFact]
    public async Task AcquireStartEgoGifts_UnknownDungeonId_Returns500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        // Unrecognised dungeonid -> StoryMdMapGen.GenerateNewFloor's battlePool-empty guard
        // throws KeyNotFoundException. Without the try/catch in the handler this would HANG
        // the request thread forever (GenerateBattleNode's retry loop is unbounded).
        await SetStoryMdSave(f, name, new StoryMirrorSaveInfo { dungeonid = 123456 });

        var resp = await client.PostAsJsonAsync("/api/AcquireStartEgoGiftsStoryMirrorDungeon", Body(jwt, new
        {
            selectedSetId = 0,
            selectedEgoGiftIds = new long[0],
        }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [SkippableFact]
    public async Task EnterStoryMirrorDungeonMapNode_FillsShopPoolAndEchoesNode()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        await SetStoryMdSave(f, name, new StoryMirrorSaveInfo { dungeonid = 910301 });

        var resp = await client.PostAsJsonAsync("/api/EnterStoryMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 3, s = 1, nid = 999 },
            abnormalityids = new long[0],
            participatedPIds = new long[0],
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var current = doc.RootElement.GetProperty("result").GetProperty("currentNode");
            Assert.Equal(3, current.GetProperty("f").GetInt64());
            Assert.Equal(1, current.GetProperty("s").GetInt64());
            Assert.Equal(999, current.GetProperty("nid").GetInt64());
        }

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(4, stored.currentinfo.shop.egpool.Count);
        // The request's currentnode is echoed in the response but NOT written to the save.
        Assert.Equal(0, stored.currentinfo.cn.nid);
    }

    [SkippableFact]
    public async Task UpdateStoryMirrorDungeonMapNode_UsesMapNodeEidWhenPceEmpty()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        // Real static-data: abnormality-event id 101001, actionEvent with nextEventID -1
        // (see StoryDungeonHandlerTests for the same fixture id).
        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.map.ns.Add(new Ns { nid = 5001, e = 3, eid = 101001 });
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/UpdateStoryMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 0, nid = 5001 },
            choiceEventData = new { sl = new long[] { 0 }, cs = 0, ri = 0 },
            dungeonUnitList = Array.Empty<object>(),
            updatedEgoGifts = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        var pce = Assert.Single(stored.currentinfo.pce);
        Assert.Equal(-1, pce.cs);
        Assert.Equal(0, pce.ri);
        Assert.Equal(new List<long> { 0 }, pce.sl);
    }

    [SkippableFact]
    public async Task UpdateStoryMirrorDungeonMapNode_NoEventIdResolvable_Returns500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        // Empty pce AND no map.ns node with a matching nid -> neither source resolves an
        // event id. Unlike the story-DUNGEON sibling, this does NOT fall back to -1.
        await SetStoryMdSave(f, name, new StoryMirrorSaveInfo { dungeonid = 910301 });

        var resp = await client.PostAsJsonAsync("/api/UpdateStoryMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 0, nid = 424242 },
            choiceEventData = new { sl = Array.Empty<long>(), cs = 0, ri = 0 },
            dungeonUnitList = Array.Empty<object>(),
            updatedEgoGifts = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [SkippableFact]
    public async Task ExitStoryMirrorDungeonMapNode_UnknownNode_Returns400()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.cn = new Currentnode { f = 0, s = 0, nid = 9999 };
        save.currentinfo.dul.Add(new Dungeonunitlist2 { pid = 424242 });
        save.currentinfo.cost = 12345;
        save.currentinfo.pce.Add(new ChoiceEventData { sl = new() { 0 } });
        save.currentinfo.shop.peg.Add(6789);
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/ExitStoryMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 0, nid = 424242 },
            dungeonunitlist = Array.Empty<object>(),
            isupdatedEgoSkillStock = 0,
            egoSkillStockList = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        // Nothing should be persisted on the error path: the handler mutates its in-memory
        // save (cn, dul, pce.Clear, shop.peg.Clear) before the node lookup that 400s, so this
        // proves the 400 happened before AccountFields.Set/SaveAsync, not just that it happened.
        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(9999, stored.currentinfo.cn.nid);
        var unit = Assert.Single(stored.currentinfo.dul);
        Assert.Equal(424242, unit.pid);
        Assert.Equal(12345, stored.currentinfo.cost);
        Assert.Single(stored.currentinfo.pce);
        Assert.Single(stored.currentinfo.shop.peg);
    }

    [SkippableFact]
    public async Task ExitStoryMirrorDungeonMapNode_UpdatesStateAndAddsCost()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.map.ns.Add(new Ns { nid = 7001, e = 1, eid = 55555 }); // plain battle, neither 5 nor 2
        save.currentinfo.pce.Add(new ChoiceEventData { sl = new() { 0 } });
        save.currentinfo.shop.peg.Add(1234);
        save.currentinfo.cost = 10;
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/ExitStoryMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 0, nid = 7001 },
            dungeonunitlist = new[]
            {
                new { sp = 1, upidx = Array.Empty<long>(), mlos = 0, pid = 77, ch = 2, cm = 3, mhos = 4, g = 5, l = 6, es = Array.Empty<object>(), isp = 7 },
            },
            isupdatedEgoSkillStock = 1,
            egoSkillStockList = new[] { new { t = "CR", n = 3 } },
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(7001, stored.currentinfo.cn.nid);
        var unit = Assert.Single(stored.currentinfo.dul);
        Assert.Equal(77, unit.pid);
        var ess = Assert.Single(stored.currentinfo.ess);
        Assert.Equal("CR", ess.t);
        Assert.Equal(3, ess.n);
        Assert.Equal(1, stored.currentinfo.nr);
        Assert.Empty(stored.currentinfo.pce);
        Assert.Empty(stored.currentinfo.shop.peg);
        Assert.Equal(55555, stored.currentinfo.eid);
        Assert.Equal(10 + MdCost.GetDefaultCost(1, 0), stored.currentinfo.cost);
    }

    [SkippableFact]
    public async Task ExitStoryMirrorDungeonMapNode_SkipsEgoSkillStockWhenFlagIsZero()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.map.ns.Add(new Ns { nid = 7002, e = 1, eid = 1 });
        save.currentinfo.ess.Add(new EgoSkillStock { t = "VI", n = 99 });
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/ExitStoryMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 0, nid = 7002 },
            dungeonunitlist = Array.Empty<object>(),
            isupdatedEgoSkillStock = 0,
            egoSkillStockList = new[] { new { t = "CR", n = 3 } },
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        var ess = Assert.Single(stored.currentinfo.ess);
        Assert.Equal("VI", ess.t);
        Assert.Equal(99, ess.n);
    }

    [SkippableFact]
    public async Task ExitStoryMirrorDungeonMapNode_AbBattleNode_RollsEgoGiftReward()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.map.ns.Add(new Ns { nid = 7003, e = 5, eid = 42 }); // ab-battle node
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/ExitStoryMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 0, nid = 7003 },
            dungeonunitlist = Array.Empty<object>(),
            isupdatedEgoSkillStock = 0,
            egoSkillStockList = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        var rre = Assert.Single(stored.currentinfo.rre);
        Assert.Equal("GetEgogift", rre.rt);
        Assert.Equal(1, rre.se);
        Assert.Equal(1, rre.sh);
        var giftId = Assert.Single(rre.pool);
        Assert.Contains(giftId, StoryMdThemeData.GetTheme(910301).GetCombinedEgoGiftPool());
    }
}
