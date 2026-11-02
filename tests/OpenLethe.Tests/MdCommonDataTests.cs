using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server;
using OpenLethe.Server.Auth;
using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Rules;

// mirror-dungeon-common-data uploads (MdCommonSource): every section the server reads still
// answers with the shipped numbers when nothing is uploaded, and with the uploaded ones when it is.
public class MdCommonDataTests
{
    private static IDisposable Upload(params string[] files) =>
        MdCatalog.Use(new MdCatalog(null, null, null, null,
            MdCommonSource.Overlay(files.Select(f => JsonNode.Parse(f)!.AsObject()))));

    // ---- no upload: the data-driven readers give what the hardcoded ones did ----

    [Fact]
    public void WithoutUpload_FusionTablesMatchTheShippedNumbers()
    {
        Assert.Null(MdCatalog.Current);

        static long OldTier(long score, bool super) => super
            ? score switch { >= 0 and <= 9 => 1, >= 10 and <= 14 => 2, >= 15 and <= 21 => 3, _ => 4 }
            : score switch { >= 0 and <= 10 => 1, >= 11 and <= 16 => 2, >= 17 and <= 24 => 3, _ => 4 };
        for (long score = 0; score <= 1100; score++)
        {
            Assert.Equal(OldTier(score, false), MdEgoFusion.DetermineOutputEgoTier(score, false));
            Assert.Equal(OldTier(score, true), MdEgoFusion.DetermineOutputEgoTier(score, true));
        }

        static float OldProb(int n) => n <= 2 ? 0.6f : n == 3 ? 0.9f : 0.99f;
        for (var n = 0; n <= 8; n++) Assert.Equal(OldProb(n), MdEgoFusion.DetermineMatchKeywordProb(n));

        Assert.Equal(6, MdEgoFusion.DetermineEgoCombineScore(9001)); // TIER_2
        Assert.Equal(9088, MdEgoFusion.FuseGiftFixed(new List<long> { 9003, 9053, 9157 }));
    }

    [Fact]
    public void WithoutUpload_SectionsReadTheSeasonFile()
    {
        Assert.Equal(new long[] { 9000005, 9000006, 9000007, 9000008 }, MdCommonTable.HiddenBattle.pool);
        Assert.Equal(11, MdCommonTable.HiddenBattle.minFloorCondition);
        Assert.Equal(60, MdEgoData.TierUpgradeCost(9001, 1));
        Assert.Equal(75, MdCommonTable.VictoryCosts(false).Single(c => c.encounterType == "BATTLE").cost);
        Assert.Equal(85, MdCommonTable.VictoryCosts(true).Single(c => c.encounterType == "BATTLE").cost);

        var seps = StartRunRules.NewRun(8, 0).StartPools;
        Assert.Equal(10, seps.Count);
        Assert.Equal("Combustion", seps[0].Keyword);
        Assert.Equal(new List<long> { 9001, 9009, 9103 }, seps[0].Pool);
        Assert.Equal(("Hit", 9L), (seps[9].Keyword, seps[9].SetId));
    }

    // ---- hiddenBattleInfo: pool adds, the rest replaces ----

    [Fact]
    public void HiddenBattlePool_IsAddedTo()
    {
        using (Upload("""{ "hiddenBattleInfo": { "pool": [9000099] } }"""))
        {
            Assert.Equal(new long[] { 9000005, 9000006, 9000007, 9000008, 9000099 }, MdCommonTable.HiddenBattle.pool);
            Assert.Equal(11, MdCommonTable.HiddenBattle.minFloorCondition); // not uploaded, kept
            Assert.True(MdMapGen.IsHiddenBattle(9000099));
        }
        Assert.False(MdMapGen.IsHiddenBattle(9000099));
    }

    [Fact]
    public void HiddenBattleFloorsAndOdds_AreReplaced()
    {
        using (Upload("""{ "hiddenBattleInfo": { "minFloorCondition": 1, "probInfo": [ { "floor": 1, "prob": 1 } ] } }"""))
        {
            var info = MdCommonTable.HiddenBattle;
            Assert.Equal(1.0, info.ProbForFloor(0));
            Assert.Equal(0.0, info.ProbForFloor(10)); // the shipped floors 11-15 are gone
            Assert.Equal(4, info.pool.Count);
        }
    }

    // ---- wholesale sections ----

    [Fact]
    public void UpgradeCostTable_IsReplaced()
    {
        using (Upload("""{ "egoGiftUpgradeCostTable": { "table": [ { "tier": 2, "cost": [1, 2] } ] } }"""))
        {
            Assert.Equal(1, MdEgoData.TierUpgradeCost(9001, 1));
            Assert.Equal(0, MdEgoData.TierUpgradeCost(9002, 1)); // tier 1 is no longer listed
        }
        Assert.Equal(60, MdEgoData.TierUpgradeCost(9001, 1));
    }

    [Fact]
    public void CombineTierTable_IsReplaced()
    {
        using (Upload("""
            { "egoGiftCombineTierTable": {
                "combineScoreByEgoGiftTier": [ { "egoGiftTier": 2, "combineScore": 50 } ],
                "combineTierResultByCombineScore": [ { "combineScoreRangeMin": 0, "combineScoreRangeMax": 999, "tierResult": 3 } ],
                "combineTwo": [ { "aTier": 1, "bTier": 1, "resultTier": 4 } ] } }
            """))
        {
            Assert.Equal(50, MdEgoFusion.DetermineEgoCombineScore(9001));
            Assert.Equal(3, MdEgoFusion.DetermineOutputEgoTier(5, false));
            // Not uploaded inside the section: the shipped enhanced table is gone too, so the
            // fallback (the shipped numbers) answers.
            Assert.Equal(1, MdEgoFusion.DetermineOutputEgoTier(5, true));
            Assert.Equal(4, MdEgoFusion.CombineTierTable!.combineTwo.Single().resultTier);
        }
        Assert.Equal(1, MdEgoFusion.DetermineOutputEgoTier(5, false));
    }

    [Fact]
    public void RandomCombineProbs_AreReplaced()
    {
        using (Upload("""{ "egogiftRandomCombineProbs": { "origin": [ { "materialCount": 2, "prob": 0 } ] } }"""))
            Assert.Equal(0f, MdEgoFusion.DetermineMatchKeywordProb(3));
        Assert.Equal(0.9f, MdEgoFusion.DetermineMatchKeywordProb(3));
    }

    [Fact]
    public void VictoryCosts_AreReplacedPerList()
    {
        var before = MdCost.BattleVictoryCost(1, 0, hard: false);
        using (Upload("""{ "originbattlevictorycostvalueList": [ { "encounterType": "BATTLE", "cost": 1000, "battleCostWeightsByFloorIdx": 0 } ] }"""))
        {
            Assert.True(MdCost.BattleVictoryCost(1, 0, hard: false) > before);
            Assert.Equal(0, MdCost.BattleVictoryCost(6, 0, hard: false)); // BOSS no longer listed
            Assert.Equal(85, MdCommonTable.VictoryCosts(true).Single(c => c.encounterType == "BATTLE").cost);
        }
        using (Upload("""{ "originbattlevictorycostvalueListHardLevel": [ { "encounterType": "BATTLE", "cost": 7, "battleCostWeightsByFloorIdx": 0 } ] }"""))
        {
            Assert.Equal(7, MdCommonTable.VictoryCosts(true).Single().cost);
            Assert.Equal(before, MdCost.BattleVictoryCost(1, 0, hard: false));
        }
    }

    [Fact]
    public void StartEgoGiftPools_AreReplaced_AndAnEmptyListKeepsTheShippedSets()
    {
        using (Upload("""{ "startEgoGiftPools": [ { "keyword": "Sinking", "normalpool": [9041, 9054, 990101], "buffpool": [] } ] }"""))
        {
            var seps = Assert.Single(StartRunRules.NewRun(8, 0).StartPools);
            Assert.Equal((0L, "Sinking"), (seps.SetId, seps.Keyword));
            Assert.Equal(new List<long> { 9041, 9054, 990101 }, seps.Pool);
        }
        using (Upload("""{ "startEgoGiftPools": [] }"""))
            Assert.Equal(10, StartRunRules.NewRun(8, 0).StartPools.Count);
    }

    // ---- egoGiftCombineFixedTable: recipes add ----

    [Fact]
    public void FixedRecipes_AreAddedTo()
    {
        using (Upload("""
            { "egoGiftCombineFixedTable": { "combineFixed": [
                { "aEgoGiftId": 9001, "bEgoGiftId": 9002, "resultEgoGiftId": 990101, "requiredEgoGiftIds": [9001, 9002] } ] } }
            """))
        {
            Assert.Equal(990101, MdEgoFusion.FuseGiftFixed(new List<long> { 9002, 9001 }));
            Assert.Equal(9088, MdEgoFusion.FuseGiftFixed(new List<long> { 9003, 9053, 9157 })); // shipped recipe kept
            Assert.Contains(9001L, MdEgoFusion.FusionComponents(990101));
            Assert.Contains(990101L, MdEgoFusion.EgoRecipeMapping.Values);
        }
        Assert.Empty(MdEgoFusion.FusionComponents(990101));
        Assert.DoesNotContain(990101L, MdEgoFusion.EgoRecipeMapping.Values);
    }

    [Fact]
    public void FixedRecipes_AFullCopyOfTheShippedTableAddsNothing()
    {
        var shipped = MdEgoFusion.EgoRecipeMapping.Count;
        var copy = new JsonObject
        {
            ["egoGiftCombineFixedTable"] = JsonSerializer.SerializeToNode(
                MdCommonSource.Static.View<EgoGiftCombineFixedTable>().egoGiftCombineFixedTable,
                new JsonSerializerOptions { IncludeFields = true }),
        };
        using (Upload(copy.ToJsonString()))
            Assert.Equal(shipped, MdEgoFusion.EgoRecipeMapping.Count);
    }

    // ---- merging rules ----

    [Fact]
    public void Uploads_ApplyInOrder_AndIgnoreCurrentDungeonId()
    {
        using (Upload(
            """{ "currentDungeonId": 7, "themePoolNum": 4, "hiddenBattleInfo": { "pool": [9000098] } }""",
            """{ "themePoolNum": 5, "hiddenBattleInfo": { "pool": [9000099] } }"""))
        {
            Assert.Equal(5, MdCommonTable.ThemePoolNum);
            Assert.Contains(9000098L, MdCommonTable.HiddenBattle.pool);
            Assert.Contains(9000099L, MdCommonTable.HiddenBattle.pool);
        }
    }

    [Fact]
    public void Validate_NamesASectionOfTheWrongShape()
    {
        Assert.Null(MdCommonSource.Validate(JsonNode.Parse("""{ "hiddenBattleInfo": { "pool": [1] } }""")!.AsObject()));
        Assert.NotNull(MdCommonSource.Validate(JsonNode.Parse("""{ "hiddenBattleInfo": { "pool": "nope" } }""")!.AsObject()));
        Assert.NotNull(MdCommonSource.Validate(JsonNode.Parse("""{ "egoGiftUpgradeCostTable": [1, 2] }""")!.AsObject()));
    }
}

// The upload -> catalog -> handler path end to end.
[Collection("postgres")]
public class MdCommonDataEndpointTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new
    {
        userAuth = new { authCode = jwt },
        parameters = p,
    };

    [SkippableFact]
    public async Task Upload_SkipsABadFile_AndTheGoodOneDrivesTheNextRun()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var name = $"common_{Guid.NewGuid():N}";
        string jwt;
        using (var scope = f.Services.CreateScope())
        {
            await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>())
                .GetOrCreateByUsernameAsync(name);
            jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        }
        var client = f.CreateClient();

        // Each file is one object - the client mod's shape for an unlisted table.
        var resp = await client.PostAsJsonAsync("/custom/upload/mirror-dungeon-common-data", Body(jwt, new
        {
            list = new object[]
            {
                new { currentDungeonId = 8, startEgoGiftPools = new[] { new { keyword = "Sinking", normalpool = new[] { 9041L, 9054L, 9124L }, buffpool = Array.Empty<long>() } } },
                new { hiddenBattleInfo = new { pool = "not a list" } },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
            Assert.Contains("skipped file 1", doc.RootElement.GetProperty("message").GetString());

        using (var scope = f.Services.CreateScope())
        {
            var account = (await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>())
                .FindByUsernameAsync(name))!;
            Assert.Single(AccountFields.Get<List<JsonObject>>(account.CustomMdCommonData)!);
        }

        var enter = await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 8, idx = 0 }));
        Assert.Equal(HttpStatusCode.OK, enter.StatusCode);
        using var enterDoc = JsonDocument.Parse(await enter.Content.ReadAsStringAsync());
        var seps = enterDoc.RootElement.GetProperty("result").GetProperty("saveInfo").GetProperty("currentInfo").GetProperty("seps");
        Assert.Equal(1, seps.GetArrayLength());
        Assert.Equal("Sinking", seps[0].GetProperty("keyword").GetString());

        // Per request: the process-wide view is untouched.
        Assert.Equal(10, MdCommonTable.StartEgoGiftPools.Count);
    }
}
