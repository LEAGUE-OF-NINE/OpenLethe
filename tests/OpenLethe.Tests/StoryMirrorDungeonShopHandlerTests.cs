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
}
