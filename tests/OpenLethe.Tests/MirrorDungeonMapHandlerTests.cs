using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server;
using OpenLethe.Server.Auth;
using OpenLethe.Server.Wire;

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

        // 2. SelectThemeFloor(1001) at floor 0: generates the map, records the theme, full-heals
        // the party, resets tfpsCreated, and preserves (does not clobber) startKeyword.
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
            // startKeyword was set by AcquireStart above (selectedSetId 0 -> the fixed seps
            // catalog's "Combustion", capture-verified) and is preserved here, no longer
            // overwritten by SelectThemeFloor.
            Assert.Equal("Combustion", save.GetProperty("currentInfo").GetProperty("startKeyword").GetString());
            Assert.Equal(0, save.GetProperty("currentInfo").GetProperty("tfpsCreated").GetInt64());
            // Full heal on floor start: every unit at full HP.
            foreach (var u in save.GetProperty("currentInfo").GetProperty("dul").EnumerateArray())
                Assert.Equal(10000, u.GetProperty("ch").GetInt64());
        }

        // 3. Force the generated shop node to "super shop" (eid != 0) so shop_gift_count is
        // deterministic (10), then enter it.
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
            Assert.Equal(10, result.GetProperty("shopInfo").GetProperty("slots").GetArrayLength());
            // Shop-node entry (e==10): event/shop nodes are worth nr==3.
            Assert.Equal(3, result.GetProperty("nr").GetInt64());
        }

        var final = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(10, final.currentInfo.shop.slots.Count);
        Assert.Equal(shopNode.nid, final.currentInfo.cn.nid);
    }

    [SkippableFact]
    public async Task SelectThemeFloor_NonZeroFloor_GeneratesFloorKeyedMap_HealsParty()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        // Two floors already recorded -> the floor being selected is index 2 (== tfs.Count). The
        // leveladders count is deliberately mismatched (1, not 2) to prove the floor is NOT taken
        // from leveladders. A wounded (ch>0) and a dead (ch==0) unit check the heal + revive rule.
        save.currentInfo.tfs.Add(new Tfs { f = 0, idx = 1, tfid = 1001 });
        save.currentInfo.tfs.Add(new Tfs { f = 1, idx = 1, tfid = 1001 });
        save.currentInfo.leveladders.Add(1);
        save.currentInfo.dul.Add(new Dungeonunitlist1 { pid = 1, ch = 3000, cm = 12 });
        save.currentInfo.dul.Add(new Dungeonunitlist1 { pid = 2, ch = 0, cm = 40 });
        save.currentInfo.eid = 999;
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/SelectThemeFloorMirrorDungeon",
            Body(jwt, new { selectedIdx = 2, selectedThemeFoorId = 1001 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;

        // Floor index derives from tfs.Count (2), not leveladders (1).
        Assert.Equal(2, stored.currentInfo.cn.f);
        Assert.Equal(20000, stored.currentInfo.cn.nid);
        Assert.Equal(0, stored.currentInfo.eid);            // reset on floors >= 1
        var newTfs = stored.currentInfo.tfs[^1];
        Assert.Equal(2, newTfs.f);
        Assert.Equal(1, newTfs.idx);                        // act = floor/5 + 1

        // Every generated node is keyed to floor 2 (nid in 20000..29999, f == 2); the floor has a
        // boss (e==6) and a shop (e==10) node.
        var floorNodes = stored.dungeonMap.ns.Where(n => n.nid >= 20000 && n.nid < 30000).ToList();
        Assert.NotEmpty(floorNodes);
        Assert.All(floorNodes, n => Assert.Equal(2, n.f));
        Assert.Contains(floorNodes, n => n.e == 6);
        Assert.Contains(floorNodes, n => n.e == 10);
        // choiceEventList == this floor's e==3 event-node eids.
        Assert.Equal(
            floorNodes.Where(n => n.e == 3).Select(n => n.eid).OrderBy(x => x),
            stored.choiceEventList.OrderBy(x => x));

        // Full heal: both units to 10000; the revived (was ch==0) unit's cm cleared, the wounded
        // (alive) unit's cm preserved.
        var u1 = stored.currentInfo.dul.Single(u => u.pid == 1);
        var u2 = stored.currentInfo.dul.Single(u => u.pid == 2);
        Assert.Equal(10000, u1.ch);
        Assert.Equal(12, u1.cm);
        Assert.Equal(10000, u2.ch);
        Assert.Equal(0, u2.cm);

        Assert.Equal(0, stored.currentInfo.tfpsCreated);
        Assert.Equal("None", stored.currentInfo.startKeyword); // preserved, not clobbered
    }

    // GrantEgoGift_OwnedTier2Or3_UpgradesToSuperWithOid_ElseAddsAsIs moved to MdRulesSharedTests
    // (SharedRules.GrantEgoGift on Run) - Task 12 deleted the wire-typed
    // MirrorDungeonMapEndpoints.GrantEgoGift this test used to pin.

    [SkippableFact]
    public async Task EnterMapNode_NormalShop_FiveSlots_NonShopNode_NoShopInfoAndNrFour()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        // Normal shop (eid == 0, the default) -> 5 slots, nr == 3.
        save.dungeonMap.ns.Add(new Ns { f = 0, s = 1, nid = 301, e = 10, eid = 0 });
        // Ordinary battle node (e == 1) -> no shopInfo key, nr == 4.
        save.dungeonMap.ns.Add(new Ns { f = 0, s = 2, nid = 302, e = 1, eid = 0 });
        await SetSave(f, name, save);

        var shopResp = await client.PostAsJsonAsync("/api/EnterMirrorDungeonMapNode",
            Body(jwt, new { currentnode = new { f = 0, s = 1, nid = 301 } }));
        using (var doc = JsonDocument.Parse(await shopResp.Content.ReadAsStringAsync()))
        {
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(5, result.GetProperty("shopInfo").GetProperty("slots").GetArrayLength());
            Assert.Equal(3, result.GetProperty("nr").GetInt64());
            Assert.False(result.GetProperty("changedHiddenNode").GetBoolean());
        }

        var battleResp = await client.PostAsJsonAsync("/api/EnterMirrorDungeonMapNode",
            Body(jwt, new { currentnode = new { f = 0, s = 2, nid = 302 } }));
        using (var doc = JsonDocument.Parse(await battleResp.Content.ReadAsStringAsync()))
        {
            var result = doc.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("shopInfo", out _));
            Assert.Equal(4, result.GetProperty("nr").GetInt64());
        }
    }

    [SkippableFact]
    public async Task ExitMapNode_BossNode_QueuesConfirmedBattleCaseAndEnemyBufRewards()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        // cn.f = 5 (the first hard floor): static enemy-buff pool is 992251-258, BattleRewardCase
        // shows 3, and boss stage 2066935 confirms the hidden +3 level-cap gift 993005. floor
        // (leveladders.Count) stays 0, so the cost delta is still the floor-0 boss cost (200).
        save.dungeonMap.ns.Add(new Ns { f = 5, s = 5, nid = 5505, e = 6, eid = 2066935 });
        save.currentInfo.tfs.Add(new Tfs { f = 5, idx = 2, tfid = 1108, egs = new() { 9019, 9017, 9020, 9021 } });
        save.currentInfo.dul.Add(new Dungeonunitlist1 { pid = 7, ch = 5000, cm = 0, l = 60 });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/ExitMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 5, s = 5, nid = 5505 },
            dungeonunitlist = new[] { new { pid = 7, ch = 5000, cm = 0, l = 60, upidx = Array.Empty<long>(), es = Array.Empty<object>() } },
            noderesult = 1,
            choiceEventData = new { },
            isupdatedEgoSkillStock = 0,
            egoSkillStockList = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var currentInfo = doc.RootElement.GetProperty("result").GetProperty("currentInfo");
        Assert.Equal(500 + 200, currentInfo.GetProperty("cost").GetInt64());

        var rre = currentInfo.GetProperty("rre").EnumerateArray().ToList();
        // Order: leading confirmed (993005 not yet owned) -> BattleRewardCase -> EnemyBuf.
        Assert.Equal(3, rre.Count);
        Assert.Equal("GetConfirmedEgogiftOnWinBattle", rre[0].GetProperty("rt").GetString());
        Assert.Equal(993005, rre[0].GetProperty("pool")[0].GetInt64());
        Assert.Equal("GetBattleRewardCase", rre[1].GetProperty("rt").GetString());
        Assert.Equal("GetEgogiftWithEnemyBuf", rre[2].GetProperty("rt").GetString());

        // GetBattleRewardCase: sh == 3 for cn.f >= 2, pool length == sh, no two cards share a
        // reward-case group (id / 100).
        var brc = rre[1];
        Assert.Equal(3, brc.GetProperty("sh").GetInt64());
        var brcPool = brc.GetProperty("pool").EnumerateArray().Select(x => x.GetInt64()).ToList();
        Assert.Equal(3, brcPool.Count);
        Assert.Equal(brcPool.Count, brcPool.Select(id => id / 100).Distinct().Count());

        // GetEgogiftWithEnemyBuf: pool == tfs.last.egs (4), sh == pool length, pool_v2 is a
        // 4-of-N subset of the STATIC floor-5 enemy-buff pool (992251-258), pool_v3 has 4 rolls.
        var buf = rre[2];
        Assert.Equal(4, buf.GetProperty("pool").GetArrayLength());
        Assert.Equal(4, buf.GetProperty("sh").GetInt64());
        var v2 = buf.GetProperty("pool_v2").EnumerateArray().Select(x => x.GetInt64()).ToList();
        var staticPool = MdEnemyBuffPool.ForFloor(5);
        Assert.Equal(new List<long> { 992251, 992252, 992253, 992254, 992255, 992256, 992257, 992258 }, staticPool);
        Assert.Equal(4, v2.Count);
        Assert.All(v2, id => Assert.Contains(id, staticPool));
        Assert.Equal(4, buf.GetProperty("pool_v3").GetArrayLength());

        // 993005 granted into egs -> egmlos = 3. sbmlos = the start-buff formula at a floor-5
        // clear (6 floors cleared, capped at +5): base 3 + 5 = 8. Per-unit mlos = 8 + 3 = 11.
        var egIds = currentInfo.GetProperty("egs").EnumerateArray().Select(g => g.GetProperty("id").GetInt64()).ToList();
        Assert.Contains(993005L, egIds);
        Assert.Equal(3, currentInfo.GetProperty("efs").GetProperty("egmlos").GetInt64());
        Assert.Equal(8, currentInfo.GetProperty("efs").GetProperty("sbmlos").GetInt64());
        foreach (var unit in currentInfo.GetProperty("dul").EnumerateArray())
        {
            Assert.Equal(5000, unit.GetProperty("ch").GetInt64()); // no force-heal; client ch kept
            Assert.Equal(-1, unit.GetProperty("pord").GetInt64());
            Assert.Equal(11, unit.GetProperty("mlos").GetInt64());
        }

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(700, stored.currentInfo.cost);
        Assert.Contains(stored.currentInfo.egs, g => g.id == 993005);
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
        save.currentInfo.tfs.Add(new Tfs { f = 0, idx = 1, tfid = 1001, egs = new() });
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
        var rre = doc.RootElement.GetProperty("result").GetProperty("currentInfo").GetProperty("rre").EnumerateArray().ToList();
        // e==5 order: [ GetEgogift, GetBattleRewardCase ]. cn.f = 0 -> BattleRewardCase shows 2.
        Assert.Equal(2, rre.Count);
        Assert.Equal("GetEgogift", rre[0].GetProperty("rt").GetString());
        Assert.Equal("GetBattleRewardCase", rre[1].GetProperty("rt").GetString());
        Assert.Equal(2, rre[1].GetProperty("sh").GetInt64());
        Assert.Equal(2, rre[1].GetProperty("pool").GetArrayLength());

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(500 + 100, stored.currentInfo.cost); // node e=5 default cost 100
    }

    [SkippableFact]
    public async Task EventNode_ConfirmedGift_DeferredAtUpdate_RealizedAtExit()
    {
        // Task 11 e==3 lifecycle, pinned against the real event 901021 (fixture nid 202):
        // action-event option[1] grants TWO normal confirmed gifts (9021, 9134) from a
        // single choice - see md-extreme-run seq20/22.
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        save.dungeonMap.ns.Add(new Ns { f = 0, s = 2, nid = 202, e = 3, eid = 901021 });
        await SetSave(f, name, save);

        var updateResp = await client.PostAsJsonAsync("/api/UpdateMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 2, nid = 202 },
            choiceEventData = new { sl = new long[] { 1 }, cs = -1, ri = 0 },
            dungeonUnitList = Array.Empty<object>(),
            updatedEgoGifts = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        using (var doc = JsonDocument.Parse(await updateResp.Content.ReadAsStringAsync()))
        {
            var gifts = doc.RootElement.GetProperty("result").GetProperty("currentEgoGifts");
            // Normal 9xxx confirmed gifts are DEFERRED - absent from the UpdateMapNode
            // response even though ProcessEvent resolved them this call.
            Assert.DoesNotContain(gifts.EnumerateArray(), g => g.GetProperty("id").GetInt64() is 9021 or 9134);
        }

        var exitResp = await client.PostAsJsonAsync("/api/ExitMirrorDungeonMapNode", Body(jwt, new
        {
            currentnode = new { f = 0, s = 2, nid = 202 },
            dungeonunitlist = Array.Empty<object>(),
            noderesult = 1,
            choiceEventData = new { sl = Array.Empty<long>(), cs = -1, ri = -1 },
            isupdatedEgoSkillStock = 0,
            egoSkillStockList = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.OK, exitResp.StatusCode);
        using var exitDoc = JsonDocument.Parse(await exitResp.Content.ReadAsStringAsync());
        var currentInfo = exitDoc.RootElement.GetProperty("result").GetProperty("currentInfo");
        var egIds = currentInfo.GetProperty("egs").EnumerateArray().Select(g => g.GetProperty("id").GetInt64()).ToList();
        Assert.Contains(9021, egIds);
        Assert.Contains(9134, egIds);

        var rre = currentInfo.GetProperty("rre").EnumerateArray().ToList();
        Assert.Equal(2, rre.Count);
        Assert.All(rre, e => Assert.Equal("GetConfirmedEgogiftOnWinBattle", e.GetProperty("rt").GetString()));
        Assert.Equal(9021, rre[0].GetProperty("pool")[0].GetInt64());
        Assert.Equal(9134, rre[1].GetProperty("pool")[0].GetInt64());
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
