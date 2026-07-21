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
public class MirrorDungeonShopHandlerTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new { userAuth = new { authCode = jwt }, parameters = p };

    private static async Task<(string jwt, string name)> NewAccount(DbWebAppFactory f)
    {
        var name = $"mdshop_{Guid.NewGuid():N}";
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
    public async Task PurchaseHealMirrorDungeon_Idx0_HealsOneUnitAndDeductsCost()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));
        await client.PostAsJsonAsync("/api/SelectFormationMirrorDungeon", Body(jwt, new
        {
            formation = new[] { new { pervPersonalityId = 0, nextPersonalityId = 7, egos = Array.Empty<object>() } },
        }));

        var resp = await client.PostAsJsonAsync("/api/PurchaseHealMirrorDungeon", Body(jwt, new { idx = 0, pid = 7 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(400, result.GetProperty("cost").GetInt64()); // 500 - 100
        Assert.Equal(100, result.GetProperty("usedcost").GetInt64());
        var unit = result.GetProperty("dungeonUnitList")[0];
        Assert.Equal(10100, unit.GetProperty("ch").GetInt64());
        Assert.Equal(30, unit.GetProperty("cm").GetInt64());

        var acc = await GetAccount(f, name);
        var stored = AccountFields.Get<MirrorOriginSaveInfo>(acc.MdSaveInfo)!;
        Assert.Equal(400, stored.currentInfo.cost);
    }

    [SkippableFact]
    public async Task PurchaseHealMirrorDungeon_Idx1_HealsAllUnits()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));
        await client.PostAsJsonAsync("/api/SelectFormationMirrorDungeon", Body(jwt, new
        {
            formation = new[]
            {
                new { pervPersonalityId = 0, nextPersonalityId = 7, egos = Array.Empty<object>() },
                new { pervPersonalityId = 0, nextPersonalityId = 8, egos = Array.Empty<object>() },
            },
        }));

        var resp = await client.PostAsJsonAsync("/api/PurchaseHealMirrorDungeon", Body(jwt, new { idx = 1, pid = 0 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(400, result.GetProperty("cost").GetInt64());
        var dul = result.GetProperty("dungeonUnitList");
        Assert.Equal(10030, dul[0].GetProperty("ch").GetInt64());
        Assert.Equal(15, dul[0].GetProperty("cm").GetInt64());
        Assert.Equal(10030, dul[1].GetProperty("ch").GetInt64());
    }

    [SkippableFact]
    public async Task PurchaseHealMirrorDungeon_NoSave_Returns500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccount(f);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/PurchaseHealMirrorDungeon", Body(jwt, new { idx = 0, pid = 7 }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [SkippableFact]
    public async Task PurchaseEgoGiftMirrorDungeon_BuysFromEgpool()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));
        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        save.currentInfo.shop.egpool = new() { 9001 };
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/PurchaseEgoGiftMirrorDungeon", Body(jwt, new { idx = 0 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(500 - 198, result.GetProperty("cost").GetInt64());
        Assert.Equal(198, result.GetProperty("usedcost").GetInt64());
        Assert.Equal(9001, result.GetProperty("egogifts")[0].GetProperty("id").GetInt64());
        Assert.Equal(9001, result.GetProperty("shopInfo").GetProperty("peg")[0].GetInt64());

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(500 - 198, stored.currentInfo.cost);
        Assert.Single(stored.currentInfo.egs);
        Assert.Single(stored.currentInfo.shop.peg);
    }

    [SkippableFact]
    public async Task SellEgoGiftMirrorDungeon_RefundsHalfPriceAndRemoves()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));
        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        save.currentInfo.egs = new() { new AcquiredEgogifts { id = 9001 } };
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/SellEgoGiftMirrorDungeon", Body(jwt, new { id = 9001 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(500 + 99, result.GetProperty("cost").GetInt64()); // 198/2 = 99
        Assert.Equal(0, result.GetProperty("egogifts").GetArrayLength());

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(500 + 99, stored.currentInfo.cost);
        Assert.Empty(stored.currentInfo.egs);
    }

    [SkippableFact]
    public async Task UpgradeEgoGiftMirrorDungeon_RaisesUlAndDeductsCost()
    {
        db.RequireDb();
        // Ego 9001 (static-data/ego-gift-mirrordungeon/ego-gift-mirrordungeon.json) has
        // price 198 and a 3-entry upgradeDataList (ul 0,1,2), so ul 0 -> 1 is valid.
        var ego = MdEgoData.GetById(9001);
        Assert.NotNull(ego);
        Assert.NotNull(ego!.upgradeDataList);

        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));
        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        save.currentInfo.egs = new() { new AcquiredEgogifts { id = 9001, ul = 0 } };
        await SetSave(f, name, save);

        var expectedCost = MdEgoData.UpgradeCost(198, 1);
        var resp = await client.PostAsJsonAsync("/api/UpgradeEgoGiftMirrorDungeon", Body(jwt, new { egoGiftId = 9001 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(500 - expectedCost, result.GetProperty("cost").GetInt64());
        Assert.Equal(expectedCost, result.GetProperty("usedcost").GetInt64());
        Assert.Equal(1, result.GetProperty("egoGift").GetProperty("ul").GetInt64());

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(1, stored.currentInfo.egs[0].ul);
    }

    [SkippableFact]
    public async Task UpgradeEgoGiftMirrorDungeon_UnknownEgoId_Returns500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var resp = await client.PostAsJsonAsync("/api/UpgradeEgoGiftMirrorDungeon", Body(jwt, new { egoGiftId = 999999999 }));
        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [SkippableFact]
    public async Task AcquireRewardEgoGiftsMirrorDungeon_AcquiresSelectedPoolEntryAndClearsRre()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));
        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        save.currentInfo.rre = new() { new RemainRewardEvent { rt = "GetEgogift", pool = new() { 9001 } } };
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/AcquireRewardEgoGiftsMirrorDungeon", Body(jwt, new { selectIndexList = new[] { 0 } }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(9001, result.GetProperty("egoGifts")[0].GetProperty("id").GetInt64());
        Assert.Equal(0, result.GetProperty("remainRewardEvent").GetArrayLength());

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Single(stored.currentInfo.egs);
        Assert.Equal(9001, stored.currentInfo.egs[0].id);
        Assert.Empty(stored.currentInfo.rre);
    }

    [SkippableFact]
    public async Task RejectRewardEgoGiftsMirrorDungeon_ClearsRre()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));
        var save = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        save.currentInfo.rre = new() { new RemainRewardEvent { rt = "GetEgogift", pool = new() { 9001 } } };
        await SetSave(f, name, save);

        var resp = await client.PostAsJsonAsync("/api/RejectRewardEgoGiftsMirrorDungeon", Body(jwt, new { }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal(0, doc.RootElement.GetProperty("result").GetProperty("remainRewardEvent").GetArrayLength());

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Empty(stored.currentInfo.rre);
    }

    [SkippableFact]
    public async Task AcquireMirrorDungeonExitReward_ClearsMdSaveInfo()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var resp = await client.PostAsJsonAsync("/api/AcquireMirrorDungeonExitReward", Body(jwt, new { }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var acc = await GetAccount(f, name);
        Assert.Equal("{}", acc.MdSaveInfo);
    }

    [SkippableFact]
    public async Task SelectFormationMirrorDungeon_BuildsDulAndSetsStartBufPoint()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var resp = await client.PostAsJsonAsync("/api/SelectFormationMirrorDungeon", Body(jwt, new
        {
            formation = new[]
            {
                new { pervPersonalityId = 0, nextPersonalityId = 7, egos = new[] { new { prevEgoId = 0, nextEgoId = 9001 } } },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var save = doc.RootElement.GetProperty("result").GetProperty("saveInfo");
        var ci = save.GetProperty("currentInfo");
        Assert.Equal(120, ci.GetProperty("startBufPoint").GetInt64());
        var unit = ci.GetProperty("dul")[0];
        Assert.Equal(7, unit.GetProperty("pid").GetInt64());
        Assert.Equal(10000, unit.GetProperty("ch").GetInt64());
        Assert.Equal(9001, unit.GetProperty("es")[0].GetProperty("id").GetInt64());

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(120, stored.currentInfo.startBufPoint);
        Assert.Single(stored.currentInfo.dul);
        Assert.Equal(7, stored.currentInfo.dul[0].pid);
    }

    [SkippableFact]
    public async Task PurchaseFormationMirrorDungeon_SwapsUnitAndEgoIdsAndDeductsCost()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));
        await client.PostAsJsonAsync("/api/SelectFormationMirrorDungeon", Body(jwt, new
        {
            formation = new[]
            {
                new { pervPersonalityId = 0, nextPersonalityId = 7, egos = new[] { new { prevEgoId = 0, nextEgoId = 9001 } } },
            },
        }));

        var resp = await client.PostAsJsonAsync("/api/PurchaseFormationMirrorDungeon", Body(jwt, new
        {
            formation = new[]
            {
                new { pervPersonalityId = 7, nextPersonalityId = 8, egos = new[] { new { prevEgoId = 9001, nextEgoId = 9002 } } },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(400, result.GetProperty("cost").GetInt64()); // 500 - 100
        Assert.Equal(100, result.GetProperty("usedcost").GetInt64());
        var unit = result.GetProperty("dungeonUnitList")[0];
        Assert.Equal(8, unit.GetProperty("pid").GetInt64());
        Assert.Equal(9002, unit.GetProperty("es")[0].GetProperty("id").GetInt64());

        var stored = AccountFields.Get<MirrorOriginSaveInfo>((await GetAccount(f, name)).MdSaveInfo)!;
        Assert.Equal(400, stored.currentInfo.cost);
        Assert.Equal(8, stored.currentInfo.dul[0].pid);
        Assert.Equal(9002, stored.currentInfo.dul[0].es[0].id);
    }
}
