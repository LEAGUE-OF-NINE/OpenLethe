using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server;
using OpenLethe.Server.Auth;
using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.MirrorDungeon.Rules;
using OpenLethe.Server.Wire;

// MdCatalog: the account's uploaded packs, gifts and drop pools laid over the static tables.
// RNG uses Random.Shared - assert membership, never exact ids.
public class MdCatalogTests
{
    private const long CustomGift = 990101;
    private const long CustomPack = 990001;

    private static MdEgo Gift(long id, string tier = "TIER_1", string keyword = "Combustion") =>
        new() { id = id, price = 150, tag = new() { tier }, keyword = keyword };

    private static long SeasonDungeonId => MdThemeData.DropPools().Max(p => p.dungeonId);

    // A built-in pack whose floor shop draws from the season pool.
    private static long StaticPack => MdThemeData.AllThemes().First().id;

    [Fact]
    public void WithoutACatalog_OnlyTheStaticTablesAnswer()
    {
        Assert.Null(MdCatalog.Current);
        Assert.Null(MdEgoData.GetById(CustomGift));
        Assert.Null(MdThemeData.GetThemeById(CustomPack));
    }

    [Fact]
    public void UploadedGift_IsLookedUpLikeABuiltInOne_AndOnlyWhileCurrent()
    {
        using (MdCatalog.Use(new MdCatalog(null, new[] { Gift(CustomGift, "TIER_3") }, null)))
        {
            Assert.Equal(150, MdEgoData.GetById(CustomGift)!.price);
            Assert.Equal(3, MdEgoData.DetermineEgoTier(CustomGift));
            Assert.Contains(CustomGift, MdEgoData.AllIds());
            Assert.NotNull(MdEgoData.GetById(9001)); // the static gifts are still there
        }

        Assert.Null(MdEgoData.GetById(CustomGift));
        Assert.DoesNotContain(CustomGift, MdEgoData.AllIds());
    }

    [Fact]
    public void UploadedGift_WithABuiltInId_OverridesIt()
    {
        var staticPrice = MdEgoData.GetById(9001)!.price;
        using (MdCatalog.Use(new MdCatalog(null, new[] { new MdEgo { id = 9001, price = staticPrice + 1, tag = new() { "TIER_2" } } }, null)))
        {
            Assert.Equal(staticPrice + 1, MdEgoData.GetById(9001)!.price);
            Assert.Single(MdEgoData.AllIds(), id => id == 9001);
        }
        Assert.Equal(staticPrice, MdEgoData.GetById(9001)!.price);
    }

    [Fact]
    public void UploadedDropPool_PutsTheGiftOnSaleOnBuiltInFloors()
    {
        var pool = new DungeonEgoGiftDropPool { dungeonId = SeasonDungeonId, egoGifts = new() { CustomGift } };
        using (MdCatalog.Use(new MdCatalog(null, new[] { Gift(CustomGift) }, new[] { pool })))
        {
            Assert.Contains(CustomGift, new MdThemePool().ShopEgoGiftsPool(StaticPack));
            Assert.Contains(CustomGift, new MdThemePool().GeneralGiftPool());
        }

        // The static pool was copied, never extended in place.
        Assert.DoesNotContain(CustomGift, MdThemeData.DropPools().SelectMany(p => p.egoGifts));
    }

    [Fact]
    public void UploadedGift_NotInAnyPool_NeverDrops()
    {
        using (MdCatalog.Use(new MdCatalog(null, new[] { Gift(CustomGift) }, null)))
        {
            Assert.DoesNotContain(CustomGift, new MdThemePool().ShopEgoGiftsPool(StaticPack));
            Assert.DoesNotContain(CustomGift, new MdThemePool().GeneralGiftPool());
        }
    }

    [Fact]
    public void UploadedGlobalExclude_TakesAGiftOffTheShelves()
    {
        var onSale = new MdThemePool().ShopEgoGiftsPool(StaticPack);
        Assert.NotEmpty(onSale);
        var target = onSale[0];

        var pool = new DungeonEgoGiftDropPool { dungeonId = SeasonDungeonId, globalExcludeEgoGifts = new() { target } };
        using (MdCatalog.Use(new MdCatalog(null, null, new[] { pool })))
            Assert.DoesNotContain(target, new MdThemePool().ShopEgoGiftsPool(StaticPack));

        Assert.Contains(target, new MdThemePool().ShopEgoGiftsPool(StaticPack));
    }

    [Fact]
    public void UploadedPack_IsInEveryPool_WithoutBeingPassedIn()
    {
        var pack = new ThemeStatic { id = CustomPack, egoGiftPool = new() { CustomGift } };
        using (MdCatalog.Use(new MdCatalog(new[] { pack }, new[] { Gift(CustomGift) }, null)))
        {
            Assert.Same(pack, MdThemeData.GetThemeById(CustomPack));
            Assert.Contains(CustomPack, new MdThemePool().pools.Keys);
            Assert.Contains(CustomGift, new MdThemePool().ShopEgoGiftsPool(CustomPack));
        }
        Assert.DoesNotContain(CustomPack, new MdThemePool().pools.Keys);
    }

    private static AbStage Stage(long id, params long[] giftIds) => new()
    {
        id = id,
        rewardList = giftIds.Select(g => new AbReward { rewardType = "EGO_GIFT", rewardId = g, num = 1, prob = 1 }).ToList(),
        participantInfo = new AbParticipantInfo { min = 1, max = 5 },
    };

    [Fact]
    public void UploadedBattle_ItsRewardListIsLookedUp_AndOnlyWhileCurrent()
    {
        const long stageId = 990201;
        using (MdCatalog.Use(new MdCatalog(null, null, null, new[] { Stage(stageId, CustomGift) })))
        {
            Assert.Equal(new[] { CustomGift }, MdAbRewards.GetByNodeId(stageId)!.Select(r => r.rewardId));
            Assert.Equal(5, MdAbRewards.MaxParticipants(stageId));
        }
        Assert.Null(MdAbRewards.GetByNodeId(stageId));
    }

    [Fact]
    public void UploadedBattle_OnAHardAbnoNode_GrantsItsListedGift()
    {
        const long stageId = 990201;
        RewardEngine.NodeExitResult Exit(Run run) => RewardEngine.ResolveNodeExit(
            run, new Currentnode { f = 0, s = 0, nid = 100 }, new List<Dungeonunitlist1>(), noderesult: 0,
            choiceEventData: new ChoiceEventData(), isUpdatedEgoSkillStock: false,
            egoSkillStockList: new List<EgoSkillStock>(), abnormalityLogs: new List<System.Text.Json.Nodes.JsonNode>());
        Run RunOnStage()
        {
            var run = new Run();
            run.Floor.Nodes.Add(new MapNode { Nid = 100, E = 14, Eid = stageId, F = 0 });
            return run;
        }

        var without = RunOnStage();
        Exit(without);
        Assert.DoesNotContain(without.Gifts.Items, g => g.Id == CustomGift);

        var with = RunOnStage();
        using (MdCatalog.Use(new MdCatalog(null, new[] { Gift(CustomGift) }, null, new[] { Stage(stageId, CustomGift) })))
            Exit(with);
        Assert.Contains(with.Gifts.Items, g => g.Id == CustomGift);
        Assert.Contains(with.RewardEvents, e => e.Rt == "GetConfirmedEgogiftOnWinBattle" && e.Pool!.Contains(CustomGift));
    }

    [Fact]
    public void FromColumns_ReadsThePlaceholdersAsNothing()
    {
        var catalog = MdCatalog.FromColumns("{}", "[]", null);
        Assert.True(catalog.IsEmpty);
        using (MdCatalog.Use(catalog)) Assert.Null(MdCatalog.Current);
    }
}

// The upload -> catalog -> handler path end to end.
[Collection("postgres")]
public class MdCatalogEndpointTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new
    {
        userAuth = new { authCode = jwt },
        parameters = p,
    };

    [SkippableFact]
    public async Task UploadedGiftsAndPacks_ReachTheEgoGiftRecord()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var name = $"catalog_{Guid.NewGuid():N}";
        string jwt;
        using (var scope = f.Services.CreateScope())
        {
            await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>())
                .GetOrCreateByUsernameAsync(name);
            jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        }
        var client = f.CreateClient();

        // The client mod's shape: one inner list per uploaded file, each file the game's own format
        // (fields the server does not model are dropped).
        var giftResp = await client.PostAsJsonAsync("/custom/upload/ego-gift-mirrordungeon", Body(jwt, new
        {
            list = new object[]
            {
                new { list = new object[] { new { id = 990101L, price = 150L, tag = new[] { "TIER_1" }, keyword = "Combustion", attributeType = "CRIMSON" } } },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, giftResp.StatusCode);

        var poolResp = await client.PostAsJsonAsync("/custom/upload/mirrordungeon-egogift-droppool", Body(jwt, new
        {
            list = new object[] { new { list = new object[] { new { dungeonId = 8L, egoGifts = new[] { 990101L } } } } },
        }));
        Assert.Equal(HttpStatusCode.OK, poolResp.StatusCode);

        await client.PostAsJsonAsync("/custom/upload/mirrordungeon-theme-floor", Body(jwt, new
        {
            list = new object[] { new { list = new object[] { new { id = 990001L, egoGiftPool = new[] { 990101L } } } } },
        }));

        using (var scope = f.Services.CreateScope())
        {
            var account = (await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>())
                .FindByUsernameAsync(name))!;
            var gifts = AccountFields.Get<List<MdEgo>>(account.CustomEgoGifts)!;
            Assert.Equal(150, Assert.Single(gifts).price);
            var pools = AccountFields.Get<List<DungeonEgoGiftDropPool>>(account.CustomEgoGiftDropPools)!;
            Assert.Equal(new[] { 990101L }, Assert.Single(pools).egoGifts);
        }

        var resp = await client.PostAsJsonAsync("/api/GetMirrorDungeonEgoGiftRecord", Body(jwt, new { }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Contains(990101L, result.GetProperty("acquiredegogifts").EnumerateArray().Select(e => e.GetInt64()));
        Assert.Contains(990001L, result.GetProperty("themeFloorIds").EnumerateArray().Select(e => e.GetInt64()));

        // The catalog is per request: nothing leaks into the process-wide tables.
        Assert.Null(MdEgoData.GetById(990101));
    }

    [SkippableFact]
    public async Task BattleUpload_KeepsTheStagesRewardList()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var name = $"catalog_{Guid.NewGuid():N}";
        string jwt;
        using (var scope = f.Services.CreateScope())
        {
            await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>())
                .GetOrCreateByUsernameAsync(name);
            jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        }
        var client = f.CreateClient();

        // The game's battle format: everything but id, rewardList and participantInfo is dropped.
        var resp = await client.PostAsJsonAsync("/custom/upload/battle-mirrordungeon", Body(jwt, new
        {
            list = new object[]
            {
                new { list = new object[] { new
                {
                    id = 990201L,
                    stageLevel = 40,
                    participantInfo = new { min = 1, max = 6 },
                    rewardList = new object[] { new { type = "EGO_GIFT", rewardId = 990101L, num = 1, prob = 1 } },
                } } },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var s = f.Services.CreateScope();
        var account = (await new AccountStore(s.ServiceProvider.GetRequiredService<AppDbContext>())
            .FindByUsernameAsync(name))!;
        var stage = Assert.Single(AccountFields.Get<List<AbStage>>(account.CustomMdBattles)!);
        Assert.Equal(6, stage.participantInfo!.max);
        var reward = Assert.Single(stage.rewardList);
        Assert.Equal("EGO_GIFT", reward.rewardType);
        Assert.Equal(990101, reward.rewardId);
    }
}
