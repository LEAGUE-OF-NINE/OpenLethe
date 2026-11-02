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

    [Fact]
    public void ThemeDropPool_IsThePacksOwnGiftsWithoutSeasonExcludes()
    {
        var pool = new MdThemePool().ThemeDropPool(1018);

        Assert.NotEmpty(pool);
        Assert.All(pool, id => Assert.Contains(id, MdThemeData.GetThemeById(1018)!.egoGiftPool));
        // 9156 Decamillennial Hearthflame: listed by pack 1018, excluded by the season drop pool.
        Assert.DoesNotContain(9156L, pool);
    }

    [Fact]
    public void ShopEgoGiftsPool_CustomPack_StocksItsEgoGiftPool()
    {
        // 9701 is pack 1101's exclusive: no shop stocks it unless the floor's pack lists it.
        var custom = new ThemeStatic { id = 990001, egoGiftPool = new() { 9701, 9101 } };

        Assert.Empty(new MdThemePool().ShopEgoGiftsPool(990001)); // the static-only pool cannot see it
        var shop = new MdThemePool(new[] { custom }).ShopEgoGiftsPool(990001);

        Assert.Contains(9701L, shop);
        Assert.Contains(9101L, shop);
        Assert.DoesNotContain(9702L, shop); // pack 1101's other exclusives stay out
    }

    [Fact]
    public void ShopEgoGiftsPool_BuiltInPack_StillExcludesOtherPacksExclusives()
    {
        var shop = new MdThemePool().ShopEgoGiftsPool(1102);

        Assert.Contains(9706L, shop);        // 1102's own exclusive
        Assert.DoesNotContain(9701L, shop);  // 1101's
        Assert.DoesNotContain(9156L, shop);  // season-excluded
    }

    [Fact]
    public void ShopEgoGiftsPool_HardRun_SellsTheHardOnlyGifts()
    {
        // The season table excludes its excludeEgoGifts from normal runs only
        // (excludeTargetIdxList [0]): 9164 Wobbling Keg / 9160 Ruptured Blood Sac are in the
        // season pool and hard-only (pack 1018 lists them itself, 1104 does not).
        var pool = new MdThemePool();

        var normal = pool.ShopEgoGiftsPool(1104, dungeonIdx: 0);
        Assert.DoesNotContain(9164L, normal);
        Assert.DoesNotContain(9160L, normal);

        var hard = pool.ShopEgoGiftsPool(1104, dungeonIdx: 1);
        Assert.Contains(9164L, hard);
        Assert.Contains(9160L, hard);
        Assert.DoesNotContain(9164L, pool.ThemeDropPool(1018, dungeonIdx: 0));
        Assert.Contains(9164L, pool.ThemeDropPool(1018, dungeonIdx: 1));
        // Still out in a hard run: other packs' exclusives, fusion outputs, global excludes.
        Assert.DoesNotContain(9701L, hard);
        Assert.DoesNotContain(9227L, hard); // NO_SALE
        Assert.DoesNotContain(9800L, hard); // Wealth
    }

    [Fact]
    public void ShopEgoGiftsPool_NeverSellsGlobalExcludes()
    {
        var global = MdThemeData.DropPools().OrderByDescending(p => p.dungeonId).First().globalExcludeEgoGifts;
        var shop = new MdThemePool().ShopEgoGiftsPool(1001);
        Assert.All(global, excluded => Assert.DoesNotContain(excluded, shop));
    }

    [Fact]
    public void SelectRandomShopEgos_NoKeyword_DoesNotFavourKeywordNoneGifts()
    {
        // A plain refresh sends "None": the ten "None"-keyword economy gifts (Rebate Token,
        // Pamphlet, ...) used to fill half of every shop. Drawn from the whole pool they are a
        // small share of it.
        var pool = new MdThemePool();
        var noneGifts = 0;
        var total = 0;
        for (var i = 0; i < 300; i++)
        {
            foreach (var keyword in new[] { "None", null })
            {
                var chosen = pool.SelectRandomShopEgos(1001, 4, 2, keyword);
                Assert.Equal(4, chosen.Count);
                Assert.Equal(chosen.Count, chosen.Distinct().Count());
                total += chosen.Count;
                noneGifts += chosen.Count(id => MdEgoData.GetById(id)?.keyword == "None");
            }
        }
        Assert.True(noneGifts < total * 0.15, $"{noneGifts}/{total} picks were \"None\"-keyword gifts");
    }
}
