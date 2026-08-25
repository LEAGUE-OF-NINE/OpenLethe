using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server;
using OpenLethe.Server.Auth;
using OpenLethe.Server.Wire;

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
        // Sold-out slot placed in the MIDDLE so position-preserving reroll is actually
        // exercised (the old sold-first-then-append bug would move it to index 0).
        save.currentInfo.shop.slots.Add(new ShopSlot { t = "eg", id = 9002, s = 1 }); // still-available
        save.currentInfo.shop.slots.Add(new ShopSlot { t = "eg", id = 9001, s = 0 }); // sold-out
        save.currentInfo.shop.slots.Add(new ShopSlot { t = "up", id = 10100, s = 1 }); // reroll isn't eg-only
        // make IsSuperShop resolve: current node with e == 10
        save.currentInfo.cn.nid = 5;
        save.dungeonMap.ns.Add(new Ns { nid = 5, e = 10, eid = 0 });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/RefreshShopEgoGiftsMirrorDungeon",
            Body(jwt, new { keyword = "Sinking", isOrigin = 0 }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        // capture-confirmed (task-12): the Nth refresh this floor costs 15*rc (rc=1 here,
        // the first refresh), not a flat 15 - same result for a first refresh, but rc is
        // now tracked and usedcost is the running cumulative shop-spend.
        Assert.Equal(85, stored.currentInfo.cost);
        Assert.Equal(15, stored.currentInfo.usedcost);
        Assert.Equal(1, stored.currentInfo.shop.rc);
        // Reroll is position-preserving: the sold-out slot at index 1 is untouched (id,
        // t, s all unchanged) even though it's not first or last; the two available slots
        // (regardless of type - "eg" and "up" alike) keep their position and type, only
        // their id is rerolled. Total count is unchanged.
        Assert.Equal(3, stored.currentInfo.shop.slots.Count);
        Assert.Equal("eg", stored.currentInfo.shop.slots[0].t);
        Assert.Equal(1, stored.currentInfo.shop.slots[0].s);
        Assert.Equal("eg", stored.currentInfo.shop.slots[1].t);
        Assert.Equal(9001, stored.currentInfo.shop.slots[1].id);
        Assert.Equal(0, stored.currentInfo.shop.slots[1].s);
        Assert.Equal("up", stored.currentInfo.shop.slots[2].t);
        Assert.Equal(1, stored.currentInfo.shop.slots[2].s);
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

    [SkippableFact]
    public async Task AcquireRewardEgoGiftsWithEnemyBuf_PushesLevelAdderAndBothPools()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new MirrorOriginSaveInfo();
        save.currentInfo.leveladders.Add(1);
        save.currentInfo.rre.Add(new RemainRewardEvent
        {
            rt = "GetEgogiftWithEnemyBuf",
            pool = new List<long> { 9001, 9002 },
            pool_v2 = new List<long> { 9101, 9102 },
        });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon",
            Body(jwt, new { selectIndexList = new[] { 1 }, isOrigin = 0 }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        // Two per-unit level-up amounts appended per boss clear (values battle-derived; here the
        // placeholder 1s) -> [1] + [1, 1].
        Assert.Equal(new List<long> { 1, 1, 1 }, stored.currentInfo.leveladders);
        // Grouped grant of index 1: pool[1]=9002 then pool_v2[1]=9102.
        Assert.Equal(new long[] { 9002, 9102 }, stored.currentInfo.egs.Select(e => e.id).ToArray());
        // The GetEgogiftWithEnemyBuf popup is CONSUMED (not replaced with a GetBattleRewardCase);
        // no sibling reward events remained, so rre is empty.
        Assert.Empty(stored.currentInfo.rre);
        // The boss clear rolled the next floor's 4-theme pool.
        Assert.Equal(4, stored.currentInfo.tfps.Count);
    }

    [SkippableFact]
    public async Task AcquireBattleReward_CostCard_AddsCostWithinRange()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new MirrorOriginSaveInfo();
        save.currentInfo.cost = 0;
        save.currentInfo.leveladders.Add(1);
        save.currentInfo.rre.Add(new RemainRewardEvent
        {
            rt = "GetBattleRewardCase",
            pool = new List<long> { 101 }, // COST, acquireCostMin 80 / max 120
        });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/AcquireMirrorDungeonBattleReward",
            Body(jwt, new { selectIndexList = new[] { 0 }, isOrigin = 0 }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.InRange(stored.currentInfo.cost, 80, 120);
        // Only the GetBattleRewardCase popup is consumed; nothing else was queued, so rre empties.
        Assert.Empty(stored.currentInfo.rre);
    }

    [SkippableFact]
    public async Task AcquireBattleReward_ConsumesOnlyBattleCase_KeepsSiblingsAndTfpsEmpty()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new MirrorOriginSaveInfo();
        // A boss-clear rre: a leading confirmed gift, the battle case (consumed here), and the
        // enemy-buf popup + constraints resolved by later endpoints - all must survive.
        save.currentInfo.rre.Add(new RemainRewardEvent { rt = "GetConfirmedEgogiftOnWinBattle", pool = new List<long> { 993005 } });
        save.currentInfo.rre.Add(new RemainRewardEvent { rt = "GetBattleRewardCase", pool = new List<long> { 101 } });
        save.currentInfo.rre.Add(new RemainRewardEvent { rt = "GetEgogiftWithEnemyBuf", pool = new List<long> { 9001 } });
        save.currentInfo.rre.Add(new RemainRewardEvent { rt = "GetConstraints", pool = new List<long> { 5001 } });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/AcquireMirrorDungeonBattleReward",
            Body(jwt, new { selectIndexList = new[] { 0 }, isOrigin = 0 }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(
            new[] { "GetConfirmedEgogiftOnWinBattle", "GetEgogiftWithEnemyBuf", "GetConstraints" },
            stored.currentInfo.rre.Select(re => re.rt).ToArray());
        // AcquireBattleReward does NOT roll a theme pool (that's the later WithEnemyBuf endpoint).
        Assert.Empty(stored.currentInfo.tfps);
    }

    [SkippableFact]
    public async Task AcquireBattleReward_CostEgoGiftStartCategoryCard_AddsCostWithinRange()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new MirrorOriginSaveInfo();
        save.currentInfo.cost = 0;
        save.currentInfo.leveladders.Add(1);
        save.currentInfo.rre.Add(new RemainRewardEvent
        {
            rt = "GetBattleRewardCase",
            pool = new List<long> { 201 }, // COST_EGOGIFT_START_CATEGORY, acquireCostMin 40 / max 60
        });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/AcquireMirrorDungeonBattleReward",
            Body(jwt, new { selectIndexList = new[] { 0 }, isOrigin = 0 }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.InRange(stored.currentInfo.cost, 40, 60);
        // RewardRandomEgoGift always returns null on shipped data (its drop-pool lookup
        // wants dungeonId == 5, which never exists - a preserved upstream quirk, not a bug).
        // So this card grants no ego gift and rre stays empty (replaced by newRewards).
        Assert.Empty(stored.currentInfo.egs);
        Assert.Empty(stored.currentInfo.rre);
    }

    [SkippableFact]
    public async Task AcquireBattleReward_EgoStockCard_RaisesLeastStocks()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new MirrorOriginSaveInfo();
        save.currentInfo.leveladders.Add(1);
        save.currentInfo.ess.Add(new EgoSkillStock { t = "CR", n = 50 });
        save.currentInfo.rre.Add(new RemainRewardEvent
        {
            rt = "GetBattleRewardCase",
            pool = new List<long> { 507 }, // EGOSTOCK, leastEgoStock kind 4 num 10
        });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/AcquireMirrorDungeonBattleReward",
            Body(jwt, new { selectIndexList = new[] { 0 }, isOrigin = 0 }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        // All 7 stock kinds are written back.
        Assert.Equal(7, stored.currentInfo.ess.Count);
        // CR was highest (50) so it is not among the 4 least -> unchanged.
        Assert.Equal(50, stored.currentInfo.ess.Single(s => s.t == "CR").n);
        // Exactly 4 of the 6 zero-valued kinds got +10.
        Assert.Equal(4, stored.currentInfo.ess.Count(s => s.n == 10));
    }

    [SkippableFact]
    public async Task AcquireBattleReward_OutOfRangeIndex_IsSkipped()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new MirrorOriginSaveInfo();
        save.currentInfo.cost = 7;
        save.currentInfo.leveladders.Add(1);
        save.currentInfo.rre.Add(new RemainRewardEvent { rt = "GetBattleRewardCase", pool = new List<long> { 101 } });
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/AcquireMirrorDungeonBattleReward",
            Body(jwt, new { selectIndexList = new[] { 99 }, isOrigin = 0 }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(7, stored.currentInfo.cost); // unchanged
    }

    [SkippableFact]
    public async Task AcquireBattleReward_IndexWrapsPastIntMax_IsRejectedNotWrapped()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new MirrorOriginSaveInfo();
        save.currentInfo.cost = 7;
        save.currentInfo.leveladders.Add(1);
        save.currentInfo.rre.Add(new RemainRewardEvent { rt = "GetBattleRewardCase", pool = new List<long> { 101 } });
        await SetSave(f, name, save);

        // 4294967296 == 1L << 32, wraps to 0 under an unguarded (int) cast, which would
        // wrongly hit the single COST card at index 0.
        var resp = await client.PostAsJsonAsync("/api/AcquireMirrorDungeonBattleReward",
            Body(jwt, new { selectIndexList = new[] { 4294967296L }, isOrigin = 0 }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(7, stored.currentInfo.cost); // unchanged - index rejected, not wrapped
    }
}
