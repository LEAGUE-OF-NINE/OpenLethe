using OpenLethe.Server;
using OpenLethe.Server.MirrorDungeon.Data;

// RNG uses Random.Shared - assert structural invariants (count/distinct/membership), never
// exact ids. See memory: mirror-dungeon-rng-is-nondeterministic.
public class MdThemeDataTests
{
    [Fact]
    public void AllThemes_NonEmpty_And_Theme1001Present()
    {
        var themes = MdThemeData.AllThemes();
        Assert.NotEmpty(themes);
        Assert.NotNull(MdThemeData.GetThemeById(1001));
    }

    [Fact]
    public void DropPools_NonEmpty()
    {
        Assert.NotEmpty(MdThemeData.DropPools());
    }

    [Fact]
    public void DetermineEgoTier_RealTierTaggedEgo()
    {
        // static-data/ego-gift-mirrordungeon/ego-gift-mirrordungeon.json id 9001, tag ["TIER_2"]
        Assert.Equal(2, MdEgoData.DetermineEgoTier(9001));
    }

    [Fact]
    public void DetermineEgoTier_UnknownId_ReturnsNull()
    {
        Assert.Null(MdEgoData.DetermineEgoTier(999999999));
    }

    [Fact]
    public void EgoGiftWeighted_LengthMatchesHundredTimesProb()
    {
        // ego 9001 tier 2 -> index 1; floor 0 weights [.75,.25] -> prob .25 -> weight 25
        var weighted = MdThemePool.EgoGiftWeighted(0, 9001);
        Assert.Equal(25, weighted.Count);
        Assert.All(weighted, id => Assert.Equal(9001, id));
    }

    [Fact]
    public void SelectRandomEgosFromPool_ReturnsDistinctIdsWithinThemePool()
    {
        var pool = new MdThemePool();
        var combined = MdThemePool.GetCombinedEgoGiftPool(MdThemeData.GetThemeById(1001)!).ToHashSet();

        for (var i = 0; i < 5; i++)
        {
            var chosen = pool.SelectRandomEgosFromPool(1001, 3, 0);
            Assert.True(chosen.Count <= 3);
            Assert.Equal(chosen.Count, chosen.Distinct().Count());
            Assert.All(chosen, id => Assert.Contains(id, combined));
        }
    }

    [Fact]
    public void SelectRandomEgosFromPool_UnknownTheme_ReturnsEmpty()
    {
        var pool = new MdThemePool();
        Assert.Empty(pool.SelectRandomEgosFromPool(999999999, 3, 0));
    }

    [Fact]
    public void ShopEgoGiftsPool_NonEmpty_And_ExcludesDropPoolExcludes()
    {
        var pool = new MdThemePool();
        var dropPools = MdThemeData.DropPools();
        var maxDrop = dropPools.OrderByDescending(p => p.dungeonId).First();

        var shopPool = pool.ShopEgoGiftsPool(1001);
        Assert.NotEmpty(shopPool);
        Assert.All(maxDrop.excludeEgoGifts, excluded => Assert.DoesNotContain(excluded, shopPool));
    }

    [Fact]
    public void MdAbRewards_GetByNodeId_RealNode_ReturnsRewards()
    {
        // static-data/battle-mirrordungeon/mirrordungeon-abbattle5-0.json id 2061141 ->
        // rewardList [EGO_GIFT 9014, EGO_GIFT 9103]
        var rewards = MdAbRewards.GetByNodeId(2061141);
        Assert.NotNull(rewards);
        Assert.Equal(2, rewards!.Count);
        Assert.All(rewards, r => Assert.Equal("EGO_GIFT", r.rewardType));
        Assert.Contains(rewards, r => r.rewardId == 9014);
        Assert.Contains(rewards, r => r.rewardId == 9103);
    }

    [Fact]
    public void MdAbRewards_GetByNodeId_UnknownNode_ReturnsNull()
    {
        Assert.Null(MdAbRewards.GetByNodeId(999999999));
    }

    [Fact]
    public void StartBuffLevelOffsets_ReadsEffectsFromStaticData()
    {
        // buffs [106,102]: 106 ENTER_1ST_FLOOR value 3 + CLEAR_FLOOR value 1/value2 5;
        // 102 SELECT_NEW_FLOOR_THEME value 3. This replaces the old hardcoded base-3/cap-8.
        var lo = MdStartBuffs.LevelOffsets(new long[] { 106, 102 });
        Assert.Equal(3, lo.EnterFirstFloor);
        Assert.Equal(1, lo.PerClear);
        Assert.Equal(5, lo.ClearCap);   // base 3 + cap 5 == the old hardcoded cap of 8
        Assert.Equal(3, lo.PerNewTheme);
    }

    [Fact]
    public void StartBuffSbmlosAt_MatchesCapturedProgression()
    {
        var lo = MdStartBuffs.LevelOffsets(new long[] { 106, 102 });
        Assert.Equal(3, lo.SbmlosAt(0, 0));   // enter floor 1
        Assert.Equal(4, lo.SbmlosAt(1, 0));   // floor-0 clear
        Assert.Equal(8, lo.SbmlosAt(5, 0));   // floor-4 clear (cap hit)
        Assert.Equal(8, lo.SbmlosAt(10, 0));  // clears past the cap stay 8
        Assert.Equal(11, lo.SbmlosAt(11, 1)); // floor-11 select: +1 new theme -> +3
        Assert.Equal(14, lo.SbmlosAt(13, 2)); // floor-13 select: +2 new themes -> +6
    }

    [Fact]
    public void StartBuffLevelOffsets_NonLevelBuffs_ContributeNothing()
    {
        // buff 103 (start cost/gift-select) and 107 (combat) carry no level-offset effect.
        var lo = MdStartBuffs.LevelOffsets(new long[] { 103, 107 });
        Assert.Equal(new MdLevelOffsets(0, 0, 0, 0), lo);
    }
}
