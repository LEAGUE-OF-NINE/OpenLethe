using System.Collections.Generic;
using System.Linq;
using OpenLethe.Server;
using Xunit;

namespace OpenLethe.Tests;

public class MdEgoFusionTests
{
    // Golden: ported verbatim from lethe-server models/src/mirror_dungeon/ego_gift_fusion.rs
    // test_fuse_gift_fixed. The fixed-combination hash is deterministic, so these ARE golden.
    [Theory]
    [InlineData(9088, 9003, 9053, 9157)]
    [InlineData(9090, 9042, 9089, 9161)]
    [InlineData(9167, 9112, 9115, 9166)]
    [InlineData(9171, 9023, 9064, 9170)]
    [InlineData(9176, 9049, 9125, 9175)]
    [InlineData(9098, 9056, 9097, 9179)]
    [InlineData(9184, 9062, 9072, 9183)]
    [InlineData(9765, 9709, 9710, 9711)]
    public void FuseGiftFixed_ThreeMaterials_MatchesRustGolden(long expected, long a, long b, long c)
    {
        Assert.Equal(expected, MdEgoFusion.FuseGiftFixed(new[] { a, b, c }));
    }

    [Theory]
    [InlineData(9717, 9713, 9714)]
    [InlineData(9718, 9713, 9715)]
    [InlineData(9728, 9721, 9724)]
    [InlineData(9764, 9762, 9763)]
    public void FuseGiftFixed_TwoMaterials_MatchesRustGolden(long expected, long a, long b)
    {
        Assert.Equal(expected, MdEgoFusion.FuseGiftFixed(new[] { a, b }));
    }

    [Fact]
    public void FuseGiftFixed_UnknownCombination_ReturnsNull()
    {
        Assert.Null(MdEgoFusion.FuseGiftFixed(new long[] { 1000, 2000, 3000 }));
        Assert.Null(MdEgoFusion.FuseGiftFixed(new long[] { 9003, 9053 }));
    }

    [Fact]
    public void EgoRecipeMapping_IsNonEmpty()
    {
        Assert.NotEmpty(MdEgoFusion.EgoRecipeMapping);
    }

    // Golden: ego_gift_fusion.rs test_ego_tier. Exercises the same static data.
    [Theory]
    [InlineData(9038, 3)]
    [InlineData(9704, 2)]
    [InlineData(9083, 5)]
    [InlineData(9057, 2)]
    [InlineData(9793, 4)]
    [InlineData(9117, 2)]
    [InlineData(9794, 3)]
    public void DetermineEgoTier_MatchesRustGolden(long id, long expected)
    {
        Assert.Equal(expected, MdEgoData.DetermineEgoTier(id));
    }

    [Fact]
    public void DetermineEgoTier_UnknownId_ReturnsNull()
    {
        Assert.Null(MdEgoData.DetermineEgoTier(12345));
    }

    [Theory]
    [InlineData(9038, 10)]  // tier 3
    [InlineData(9704, 6)]   // tier 2
    [InlineData(9083, 30)]  // tier 5
    [InlineData(9793, 15)]  // tier 4
    [InlineData(12345, 0)]  // unknown -> 0
    public void DetermineEgoCombineScore_MapsTierToScore(long id, long expected)
    {
        Assert.Equal(expected, MdEgoFusion.DetermineEgoCombineScore(id));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(14, 2)]
    [InlineData(15, 3)]
    [InlineData(21, 3)]
    [InlineData(22, 4)]
    [InlineData(1000, 4)]
    public void DetermineOutputEgoTier_SuperShopBoundaries(long score, long expected)
    {
        Assert.Equal(expected, MdEgoFusion.DetermineOutputEgoTier(score, true));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(10, 1)]
    [InlineData(11, 2)]
    [InlineData(16, 2)]
    [InlineData(17, 3)]
    [InlineData(24, 3)]
    [InlineData(25, 4)]
    [InlineData(1000, 4)]
    public void DetermineOutputEgoTier_NormalBoundaries(long score, long expected)
    {
        Assert.Equal(expected, MdEgoFusion.DetermineOutputEgoTier(score, false));
    }

    [Theory]
    [InlineData(0, 0.6f)]
    [InlineData(1, 0.6f)]
    [InlineData(2, 0.6f)]
    [InlineData(3, 0.9f)]
    [InlineData(4, 0.99f)]
    [InlineData(9, 0.99f)]
    public void DetermineMatchKeywordProb_MatchesRustTable(int n, float expected)
    {
        Assert.Equal(expected, MdEgoFusion.DetermineMatchKeywordProb(n));
    }

    [Fact]
    public void FuseGift_FixedRecipe_ReturnsFixedResult()
    {
        var result = MdEgoFusion.FuseGift("Sinking", false, new long[] { 9003, 9053, 9157 });
        Assert.Equal(9088, result.FixedGiftId);
    }

    // RNG path: structural only (mirror-dungeon-rng-is-nondeterministic).
    [Fact]
    public void FuseGift_RandomResult_HasTierInRangeAndKeywordOrNull()
    {
        for (var i = 0; i < 50; i++)
        {
            var result = MdEgoFusion.FuseGift("Sinking", false, new long[] { 1055, 9058, 9173 });
            Assert.Null(result.FixedGiftId);
            Assert.InRange(result.Tier, 1, 4);
            Assert.True(result.Keyword is null or "Sinking");
        }
    }

    [Fact]
    public void ShopEgoGiftsPool_ExcludesFixedRecipeOutputs()
    {
        var pool = new MdThemePool();
        var themeId = pool.pools.Keys.First();
        var gifts = pool.ShopEgoGiftsPool(themeId);
        Assert.DoesNotContain(gifts, g => MdEgoFusion.EgoRecipeMapping.Values.Contains(g));
    }

    [Fact]
    public void CombineTierTable_LoadsFromMirrorDungeonCommonData()
    {
        var table = MdEgoFusion.CombineTierTable;
        Assert.NotNull(table);
        // Verified against static-data/mirror-dungeon-common-data/mirror-dungeon-common-data-md7.json.
        Assert.Equal(6, table!.combineTwo.Count);
        Assert.Equal(10, table.combineThree.Count);

        var firstTwo = table.combineTwo[0];
        Assert.Equal(1, firstTwo.aTier);
        Assert.Equal(1, firstTwo.bTier);
        Assert.Equal(1, firstTwo.resultTier);

        var firstThree = table.combineThree[0];
        Assert.Equal(1, firstThree.aTier);
        Assert.Equal(1, firstThree.bTier);
        Assert.Equal(1, firstThree.cTier);
        Assert.Equal(1, firstThree.resultTier);
    }

    [Fact]
    public void CombineTierTable_ContainsAKnownThreeWayRow()
    {
        // {"aTier":1,"bTier":1,"cTier":2,"resultTier":2} is the second combineThree row.
        var table = MdEgoFusion.CombineTierTable!;
        Assert.Contains(table.combineThree,
            r => r.aTier == 1 && r.bTier == 1 && r.cTier == 2 && r.resultTier == 2);
    }

    // TierToInt is story-MD's own tier function and is deliberately NOT DetermineEgoTier:
    // it reads tag.First() and collapses TIER_4, TIER_5 and "no tag at all" into 4.
    [Theory]
    [InlineData(9038, 3)]   // TIER_3
    [InlineData(9704, 2)]   // TIER_2
    [InlineData(9793, 4)]   // TIER_4
    public void TierToInt_MapsLowTiersDirectly(long id, long expected)
    {
        Assert.Equal(expected, MdEgoFusion.TierToInt(id));
    }

    [Fact]
    public void TierToInt_DiffersFromDetermineEgoTier_ForTier5AndUnknown()
    {
        // 9083 is TIER_5: DetermineEgoTier says 5, TierToInt collapses it to 4.
        Assert.Equal(5, MdEgoData.DetermineEgoTier(9083));
        Assert.Equal(4, MdEgoFusion.TierToInt(9083));

        // An unknown id: DetermineEgoTier is null, TierToInt defaults to 4.
        Assert.Null(MdEgoData.DetermineEgoTier(12345));
        Assert.Equal(4, MdEgoFusion.TierToInt(12345));
    }
}
