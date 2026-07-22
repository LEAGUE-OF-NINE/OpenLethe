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
public class StoryMirrorDungeonShopHandlerTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new { userAuth = new { authCode = jwt }, parameters = p };

    private static async Task<(string jwt, string name)> NewAccount(DbWebAppFactory f)
    {
        var name = $"smdshop_{Guid.NewGuid():N}";
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
    public async Task PurchaseEgoGift_DeductsPriceAndRecordsPurchase()
    {
        db.RequireDb();
        var ego = MdEgoData.GetById(9001);
        Assert.NotNull(ego);

        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.cost = 500;
        save.currentinfo.shop.egpool = new() { 9001 };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/PurchaseEgoGiftStoryMirrorDungeon", Body(jwt, new { idx = 0 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(500 - ego!.price, result.GetProperty("cost").GetInt64());
            Assert.Equal(9001, result.GetProperty("egogifts")[0].GetProperty("id").GetInt64());
        }

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(500 - ego!.price, stored.currentinfo.cost);
        Assert.Equal(9001, Assert.Single(stored.currentinfo.shop.peg));
        Assert.Contains(stored.currentinfo.egs, e => e.id == 9001);
    }

    [SkippableFact]
    public async Task PurchaseEgoGift_IndexOutOfRange_IsNoOp()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.cost = 500;
        save.currentinfo.shop.egpool = new() { 9001 };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/PurchaseEgoGiftStoryMirrorDungeon", Body(jwt, new { idx = 99 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(500, stored.currentinfo.cost);
        Assert.Empty(stored.currentinfo.shop.peg);
        Assert.Empty(stored.currentinfo.egs);
    }

    [SkippableFact]
    public async Task PurchaseEgoGift_NoAuth_Returns401()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/PurchaseEgoGiftStoryMirrorDungeon",
            Body("not-a-real-auth-code", new { idx = 0 }));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [SkippableFact]
    public async Task SellEgoGift_AddsHalfPriceAndRemovesTheGift()
    {
        db.RequireDb();
        var ego = MdEgoData.GetById(9001);
        Assert.NotNull(ego);

        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.cost = 500;
        save.currentinfo.egs = new() { new AcquiredEgogifts { id = 9001 } };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/SellEgoGiftStoryMirrorDungeon", Body(jwt, new { id = 9001 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(500 + ego!.price / 2, result.GetProperty("cost").GetInt64());
            Assert.Equal(0, result.GetProperty("egogifts").GetArrayLength());
        }

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(500 + ego!.price / 2, stored.currentinfo.cost);
        Assert.Empty(stored.currentinfo.egs);
    }

    [SkippableFact]
    public async Task SellEgoGift_UnknownId_IsNoOp()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.cost = 500;
        save.currentinfo.egs = new() { new AcquiredEgogifts { id = 9001 } };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/SellEgoGiftStoryMirrorDungeon", Body(jwt, new { id = 424242 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(500, stored.currentinfo.cost);
        Assert.Single(stored.currentinfo.egs);
    }

    [SkippableFact]
    public async Task UpgradeEgoGift_RaisesUlAndDeductsCost()
    {
        db.RequireDb();
        // Ego 9001 (static-data/ego-gift-mirrordungeon/ego-gift-mirrordungeon.json) has
        // price 198 and a non-empty upgradeDataList, so ul 0 -> 1 is valid.
        var ego = MdEgoData.GetById(9001);
        Assert.NotNull(ego);
        Assert.NotNull(ego!.upgradeDataList);

        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.cost = 500;
        save.currentinfo.egs = new() { new AcquiredEgogifts { id = 9001, ul = 0 } };
        await SetStoryMdSave(f, name, save);

        var expectedCost = MdEgoData.UpgradeCost(ego.price, 1);
        var resp = await client.PostAsJsonAsync("/api/UpgradeEgoGiftStoryMirrorDungeon", Body(jwt, new { egoGiftId = 9001 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(500 - expectedCost, result.GetProperty("cost").GetInt64());
            Assert.Equal(1, result.GetProperty("egoGift").GetProperty("ul").GetInt64());
        }

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(500 - expectedCost, stored.currentinfo.cost);
        Assert.Equal(1, stored.currentinfo.egs[0].ul);
    }

    [SkippableFact]
    public async Task UpgradeEgoGift_UnknownEgo_Returns500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        await SetStoryMdSave(f, name, new StoryMirrorSaveInfo { dungeonid = 910301 });

        var resp = await client.PostAsJsonAsync("/api/UpgradeEgoGiftStoryMirrorDungeon", Body(jwt, new { egoGiftId = 999999999 }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [SkippableFact]
    public async Task UpgradeEgoGift_NotOwned_Returns500()
    {
        db.RequireDb();
        var ego = MdEgoData.GetById(9001);
        Assert.NotNull(ego);

        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        // 9001 resolves in static data but is not present in the save's egs.
        await SetStoryMdSave(f, name, new StoryMirrorSaveInfo { dungeonid = 910301 });

        var resp = await client.PostAsJsonAsync("/api/UpgradeEgoGiftStoryMirrorDungeon", Body(jwt, new { egoGiftId = 9001 }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [SkippableFact]
    public async Task RefreshShop_RefillsToFourAndCostsFifteen()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.cost = 500;
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/RefreshShopEgoGiftsStoryMirrorDungeon", Body(jwt, new { keyword = "" }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(500 - 15, result.GetProperty("cost").GetInt64());
        }

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(4, stored.currentinfo.shop.egpool.Count);
        Assert.Equal(500 - 15, stored.currentinfo.cost);
    }

    [SkippableFact]
    public async Task RefreshShop_MoreThanFourPurchased_DoesNotThrow()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        // D2 regression guard: peg has FIVE entries, so Rust's unguarded `4 - peg.len()`
        // underflows a usize (panic in debug, huge allocation in release). C#'s clamp must
        // return just the 5 already-purchased ids with 0 random gifts added.
        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.cost = 500;
        save.currentinfo.shop.peg = new() { 1, 2, 3, 4, 5 };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/RefreshShopEgoGiftsStoryMirrorDungeon", Body(jwt, new { keyword = "" }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(new List<long> { 1, 2, 3, 4, 5 }, stored.currentinfo.shop.egpool);
        Assert.Equal(500 - 15, stored.currentinfo.cost);
    }

    [SkippableFact]
    public async Task PurchaseHeal_SingleUnit_HealsOnlyThatUnit()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.cost = 500;
        save.currentinfo.dul = new()
        {
            new Dungeonunitlist2 { pid = 7, ch = 10000, cm = 0 },
            new Dungeonunitlist2 { pid = 8, ch = 10000, cm = 0 },
        };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/PurchaseHealStoryMirrorDungeon", Body(jwt, new { idx = 0, pid = 7 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(500 - 100, stored.currentinfo.cost);
        var matched = stored.currentinfo.dul.First(u => u.pid == 7);
        var other = stored.currentinfo.dul.First(u => u.pid == 8);
        Assert.Equal(10100, matched.ch);
        Assert.Equal(30, matched.cm);
        Assert.Equal(10000, other.ch);
        Assert.Equal(0, other.cm);
    }

    [SkippableFact]
    public async Task PurchaseHeal_AllUnits_HealsEveryUnit()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.cost = 500;
        save.currentinfo.dul = new()
        {
            new Dungeonunitlist2 { pid = 7, ch = 10000, cm = 0 },
            new Dungeonunitlist2 { pid = 8, ch = 10000, cm = 0 },
        };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/PurchaseHealStoryMirrorDungeon", Body(jwt, new { idx = 1, pid = 0 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(500 - 100, stored.currentinfo.cost);
        Assert.All(stored.currentinfo.dul, u => Assert.Equal(10030, u.ch));
        Assert.All(stored.currentinfo.dul, u => Assert.Equal(15, u.cm));
    }

    [SkippableFact]
    public async Task PurchaseHeal_UnknownIdx_DoesNotSave()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.cost = 500;
        save.currentinfo.dul = new() { new Dungeonunitlist2 { pid = 7, ch = 10000, cm = 0 } };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/PurchaseHealStoryMirrorDungeon", Body(jwt, new { idx = 7, pid = 7 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(0, result.GetProperty("cost").GetInt64());
            Assert.Equal(0, result.GetProperty("dungeonUnitList").GetArrayLength());
        }

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Equal(500, stored.currentinfo.cost);
        var unit = Assert.Single(stored.currentinfo.dul);
        Assert.Equal(10000, unit.ch);
        Assert.Equal(0, unit.cm);
    }

    [SkippableFact]
    public async Task AcquireRewardEgoGifts_GrantsSelectedGiftAndClearsRewards()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.rre = new()
        {
            new RemainRewardEvent { rt = "GetEgogift", pool = new() { 9001, 9002 } },
            new RemainRewardEvent { rt = "Other" },
        };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/AcquireRewardEgoGiftsStoryMirrorDungeon",
            Body(jwt, new { selectIndexList = new[] { 1 } }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(9002, result.GetProperty("egoGifts")[0].GetProperty("id").GetInt64());
            Assert.Equal(0, result.GetProperty("remainRewardEvent").GetArrayLength());
        }

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Contains(stored.currentinfo.egs, e => e.id == 9002);
        Assert.Empty(stored.currentinfo.rre);
    }

    [SkippableFact]
    public async Task AcquireRewardEgoGifts_NoGetEgogiftEvent_Returns500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.rre = new() { new RemainRewardEvent { rt = "Other" } };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/AcquireRewardEgoGiftsStoryMirrorDungeon",
            Body(jwt, new { selectIndexList = new[] { 0 } }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [SkippableFact]
    public async Task AcquireRewardEgoGifts_IndexOutOfRange_Returns500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        // D3 guard: Rust does `event.pool.get(index).unwrap()` on a client-supplied index,
        // which panics when out of range. Pool has 1 entry; index 5 must 500, not throw.
        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.rre = new() { new RemainRewardEvent { rt = "GetEgogift", pool = new() { 9001 } } };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/AcquireRewardEgoGiftsStoryMirrorDungeon",
            Body(jwt, new { selectIndexList = new[] { 5 } }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Empty(stored.currentinfo.egs);
        var rre = Assert.Single(stored.currentinfo.rre);
        Assert.Equal("GetEgogift", rre.rt);
        Assert.Equal(new List<long> { 9001 }, rre.pool);
    }

    [SkippableFact]
    public async Task AcquireRewardEgoGifts_EmptySelectList_DefaultsToIndexZero()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.rre = new() { new RemainRewardEvent { rt = "GetEgogift", pool = new() { 9001, 9002 } } };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/AcquireRewardEgoGiftsStoryMirrorDungeon",
            Body(jwt, new { selectIndexList = Array.Empty<int>() }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.Contains(stored.currentinfo.egs, e => e.id == 9001);
    }

    [SkippableFact]
    public async Task CombineEgoGift_RemovesAllMaterialCopiesAndGrantsAResult()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        // 9002 is deliberately duplicated - Rust's `retain` is a bulk filter, unlike the
        // plain-MD combine's first-match-each removal.
        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.egs = new()
        {
            new AcquiredEgogifts { id = 9002 },
            new AcquiredEgogifts { id = 9002 },
            new AcquiredEgogifts { id = 9001 },
        };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/CombineEgoGiftStoryMirrorDungeon",
            Body(jwt, new { materialEgoGiftIds = new[] { 9002, 9001 }, keyword = "" }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<StoryMirrorSaveInfo>((await GetAccount(f, name)).StoryMdSaveInfo)!;
        Assert.DoesNotContain(stored.currentinfo.egs, e => e.id == 9002);
        Assert.DoesNotContain(stored.currentinfo.egs, e => e.id == 9001);
        Assert.Single(stored.currentinfo.egs);
    }

    /// Reproduces the handler's tier-table lookup so tests assert against the live static
    /// data rather than a hard-coded row (mirrors the memory note: MD data may change).
    private static long ExpectedResultTier(List<long> tiers)
    {
        var table = MdEgoFusion.CombineTierTable;
        if (table is null) return 1;
        if (tiers.Count == 2)
        {
            var row = table.combineTwo.FirstOrDefault(c => c.aTier == tiers[0] && c.bTier == tiers[1]);
            return row?.resultTier ?? 1;
        }
        if (tiers.Count == 3)
        {
            var row = table.combineThree.FirstOrDefault(c => c.aTier == tiers[0] && c.bTier == tiers[1] && c.cTier == tiers[2]);
            return row?.resultTier ?? 1;
        }
        return 1;
    }

    [SkippableFact]
    public async Task CombineEgoGift_TwoMaterials_ResultMatchesTheTierTable()
    {
        db.RequireDb();
        // 9002 is TIER_1, 9001 is TIER_2 (static-data/ego-gift-mirrordungeon). Sorted tiers
        // [1, 2] hit the egoGiftCombineTierTable.combineTwo row {aTier:1, bTier:2}.
        var tiers = new List<long> { MdEgoFusion.TierToInt(9002), MdEgoFusion.TierToInt(9001) };
        tiers.Sort();
        var expectedTier = ExpectedResultTier(tiers);

        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.egs = new() { new AcquiredEgogifts { id = 9002 }, new AcquiredEgogifts { id = 9001 } };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/CombineEgoGiftStoryMirrorDungeon",
            Body(jwt, new { materialEgoGiftIds = new[] { 9002, 9001 }, keyword = "" }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var resultId = doc.RootElement.GetProperty("result").GetProperty("resultEgoGift").GetProperty("id").GetInt64();
            Assert.Equal(expectedTier, MdEgoFusion.TierToInt(resultId));
        }
    }

    [SkippableFact]
    public async Task CombineEgoGift_ThreeMaterials_ResultMatchesTheTierTable()
    {
        db.RequireDb();
        // 9002 = TIER_1, 9001 = TIER_2, 1052 = TIER_3. Sorted tiers [1, 2, 3] hit
        // egoGiftCombineTierTable.combineThree row {aTier:1, bTier:2, cTier:3}.
        var tiers = new List<long>
        {
            MdEgoFusion.TierToInt(9002),
            MdEgoFusion.TierToInt(9001),
            MdEgoFusion.TierToInt(1052),
        };
        tiers.Sort();
        var expectedTier = ExpectedResultTier(tiers);

        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.egs = new()
        {
            new AcquiredEgogifts { id = 9002 },
            new AcquiredEgogifts { id = 9001 },
            new AcquiredEgogifts { id = 1052 },
        };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/CombineEgoGiftStoryMirrorDungeon",
            Body(jwt, new { materialEgoGiftIds = new[] { 9002, 9001, 1052 }, keyword = "" }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var resultId = doc.RootElement.GetProperty("result").GetProperty("resultEgoGift").GetProperty("id").GetInt64();
            Assert.Equal(expectedTier, MdEgoFusion.TierToInt(resultId));
        }
    }

    [SkippableFact]
    public async Task CombineEgoGift_TwoUnknownMaterials_BothMapToTierOneViaOuterFallback()
    {
        db.RequireDb();
        // Ids absent from static data go through the handler's OUTER fallback (Rust's
        // extract_ego_tiers .unwrap_or(1)) rather than MdEgoFusion.TierToInt's inner "no tag ->
        // 4" arm, so with 2 unknown materials the lookup is [1, 1], not [4, 4].
        // NOTE: this test previously asserted the same numeric outcome for the wrong reason -
        // the pre-fix handler produced tiers [4, 4] (no combineTwo row -> resultTier defaults to
        // 1) which coincidentally agreed with the correct [1, 1] path (which also happens to
        // resolve to tier 1). It could not distinguish the buggy code from the fixed code. See
        // CombineEgoGift_UnknownAndTierThreeMaterial_UsesOuterFallbackForUnknownOnly below for a
        // case that actually fails on the old code.
        var unknownA = 700001001;
        var unknownB = 700001002;
        Assert.Null(MdEgoData.GetById(unknownA));
        Assert.Null(MdEgoData.GetById(unknownB));
        var expectedTier = ExpectedResultTier(new List<long> { 1, 1 });

        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.egs = new() { new AcquiredEgogifts { id = unknownA }, new AcquiredEgogifts { id = unknownB } };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/CombineEgoGiftStoryMirrorDungeon",
            Body(jwt, new { materialEgoGiftIds = new[] { unknownA, unknownB }, keyword = "" }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var resultId = doc.RootElement.GetProperty("result").GetProperty("resultEgoGift").GetProperty("id").GetInt64();
            Assert.Equal(expectedTier, MdEgoFusion.TierToInt(resultId));
        }
    }

    [SkippableFact]
    public async Task CombineEgoGift_UnknownAndTierThreeMaterial_UsesOuterFallbackForUnknownOnly()
    {
        db.RequireDb();
        // Regression test for the two-nested-fallbacks bug: an id absent from static data must
        // map to tier 1 (Rust's outer .unwrap_or(1)), not tier 4 (MdEgoFusion.TierToInt's inner
        // "no recognised tag" arm). Sorted with a genuine TIER_3 material this is [1, 3]. Under
        // the pre-fix handler (which fed the unknown id straight through TierToInt) it would
        // instead be [3, 4], which has no combineTwo row and defaults to tier 1 - masking the
        // bug for the "both unknown" case above but not for this mixed case, whose expected
        // tier (from the table) differs between [1, 3] and [3, 4].
        var unknownMaterial = 700001003;
        Assert.Null(MdEgoData.GetById(unknownMaterial));

        var tierThreeMaterial = MdEgoData.AllIds().First(id => MdEgoFusion.TierToInt(id) == 3);

        var expectedTier = ExpectedResultTier(new List<long> { 1, 3 });

        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.egs = new()
        {
            new AcquiredEgogifts { id = unknownMaterial },
            new AcquiredEgogifts { id = tierThreeMaterial },
        };
        await SetStoryMdSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/CombineEgoGiftStoryMirrorDungeon",
            Body(jwt, new { materialEgoGiftIds = new[] { unknownMaterial, tierThreeMaterial }, keyword = "" }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
        {
            var resultId = doc.RootElement.GetProperty("result").GetProperty("resultEgoGift").GetProperty("id").GetInt64();
            Assert.Equal(expectedTier, MdEgoFusion.TierToInt(resultId));
        }
    }
}
