using OpenLethe.Server;
using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Mapping;
using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.MirrorDungeon.Rules;
using OpenLethe.Server.Wire;

namespace OpenLethe.Tests;

// The start buffs' ("Grace of the Star") server-side effects, one buff family at a time, against
// the real mirrordungeon-start-buffs static data. Ids: 1xx = base, 2xx = +, 3xx = ++.
//   100 Star of the Beginning    101 Cumulating Starcloud   102 Interstellar Travel
//   103 Star-shower              104 Binary Star-shop       105 Moon Star-shop
//   106 Favor of the Nebulae     107 Starlight Guidance     108 Chance Comet
//   109 Perfected Possibility
public class MdRulesStartBuffTests
{
    private static Run RunWith(params long[] buffIds) =>
        new() { StartBuffIds = buffIds.ToList(), StartBuffsChosen = true };

    // A fresh run (starting-gift catalog, 200 Cost) with the picks stored, as EnableStartBuff leaves it.
    private static Run StartedRun(params long[] buffIds)
    {
        var run = StartRunRules.NewRun(StartRunRules.CurrentDungeonId, 0);
        run.StartBuffIds = buffIds.ToList();
        run.StartBuffsChosen = true;
        return run;
    }

    private static long Tier(long giftId) => MdEgoData.DetermineEgoTier(giftId) ?? 0;

    // ---- which buffs count ----

    [Fact]
    public void Effective_KeepsOneLevelPerBuff_TheHighest()
    {
        var effective = MdStartBuffs.Effective(new long[] { 106, 206, 102 });

        Assert.Equal(new long[] { 102, 206 }, effective.Select(b => b.id).OrderBy(id => id));
    }

    [Fact]
    public void EnabledIds_EmptyStoredPick_MeansNoBuffs_NotTheReplayFallback()
    {
        var none = RunWith();
        var neverStored = new Run();

        Assert.Empty(StartBuffEffects.EnabledIds(none));
        Assert.Equal(new long[] { 106, 102 }, StartBuffEffects.EnabledIds(neverStored));
        Assert.Equal(0, StartBuffEffects.LevelOffsets(none).EnterFirstFloor);
    }

    [Fact]
    public void EnableStartBuff_EmptyPick_SurvivesTheWireRoundTrip()
    {
        var run = new Run();
        StartRunRules.EnableStartBuff(run, new List<long>(), enableConvertedCost: false);

        var back = WireMapper.ToDomain(WireMapper.ToWire(run));

        Assert.True(back.StartBuffsChosen);
        Assert.Empty(back.StartBuffIds);
    }

    // ---- theme packs ----

    [Theory]
    [InlineData(new long[] { }, 3)]               // themePoolNum
    [InlineData(new long[] { 100 }, 4)]
    [InlineData(new long[] { 100, 102 }, 5)]
    [InlineData(new long[] { 300, 302, 309 }, 5)] // three +1s still stop at 5
    public void ThemePoolSize_Is3PlusSlots_UpTo5(long[] buffs, int expected)
    {
        Assert.Equal(expected, StartBuffEffects.ThemePoolSize(RunWith(buffs)));
    }

    [Fact]
    public void ThemePoolSize_UnstoredRun_KeepsTheCapturesFour()
    {
        Assert.Equal(4, StartBuffEffects.ThemePoolSize(new Run()));
    }

    [Fact]
    public void AcquireStart_OffersThemePoolSizePerGroup()
    {
        var run = StartedRun();
        StartRunRules.AcquireStartAndCreateThemePool(run, 0, new List<long> { 9001 }, false);

        Assert.Equal(3, run.ThemePools.Count(t => t.Idx == 0));
        Assert.Equal(3, run.ThemePools.Count(t => t.Idx == 1));
    }

    [Theory]
    [InlineData(new long[] { }, 1)]
    [InlineData(new long[] { 102 }, 3)]
    [InlineData(new long[] { 302 }, 5)]
    public void RecreateThemePool_StopsAtTheRefreshAllowance(long[] buffs, int allowed)
    {
        var run = RunWith(buffs);

        for (var i = 0; i < allowed; i++) Assert.True(MapGenerator.RecreateThemePool(run));
        var poolBefore = run.ThemePools;
        Assert.False(MapGenerator.RecreateThemePool(run));

        Assert.Equal(allowed, run.TfpsCreated);
        Assert.Same(poolBefore, run.ThemePools);
    }

    // ---- level caps ----

    [Fact]
    public void FavorOfTheNebulae_Levels()
    {
        var plus = StartBuffEffects.LevelOffsets(RunWith(206));
        Assert.Equal((3L, 1L, 9L, 0L), (plus.EnterFirstFloor, plus.PerClear, plus.ClearCap, plus.EnterSixthFloor));

        var plusPlus = StartBuffEffects.LevelOffsets(RunWith(306));
        Assert.Equal(3, plusPlus.EnterSixthFloor);
        Assert.Equal(3 + 5, plusPlus.SbmlosAt(5, 0));      // floor-5 clear: not on floor 6 yet
        Assert.Equal(3 + 6 + 3, plusPlus.SbmlosAt(6, 0));  // floor-6 clear keeps the floor-6 bonus
    }

    [Fact]
    public void InterstellarTravel_NewThemeLevels_StopAt9()
    {
        var offsets = StartBuffEffects.LevelOffsets(RunWith(102));

        Assert.Equal(9, offsets.NewThemeBonus(3));
        Assert.Equal(9, offsets.NewThemeBonus(5));
    }

    // ---- cost ----

    [Theory]
    [InlineData(101, 500, 550)]   // +10% = 50
    [InlineData(101, 2000, 2100)] // +10% = 200, max 100
    [InlineData(301, 400, 520)]   // +30% = 120
    [InlineData(301, 900, 1050)]  // +30% = 270, max 150
    public void CumulatingStarcloud_NextFloorCostInterest(long buff, long cost, long expected)
    {
        var run = RunWith(buff);
        run.Cost = cost;

        StartBuffEffects.OnMoveToNextFloor(run);

        Assert.Equal(expected, run.Cost);
    }

    [Theory]
    [InlineData(new long[] { }, 100)]
    [InlineData(new long[] { 104 }, 120)]
    [InlineData(new long[] { 304 }, 140)]
    public void BinaryStarShop_EncounterRewardCost(long[] buffs, long expected)
    {
        Assert.Equal(expected, MdCost.ApplyCostGifts(RunWith(buffs), 1, 100));
    }

    [Fact]
    public void BinaryStarShop_AddsToGoldenUrnsRate()
    {
        var run = RunWith(204); // +30%
        run.Gifts.Items.Add(new EgoGift { Id = 9079 }); // Golden Urn +20%

        Assert.Equal(150, MdCost.ApplyCostGifts(run, 1, 100));
    }

    [Theory]
    [InlineData(103, 0)]
    [InlineData(203, 75)]
    [InlineData(303, 150)]
    public void StarShower_StarlightCardGivesCost(long buff, long expected)
    {
        var run = RunWith(buff);
        run.RewardEvents.Add(new RewardEvent { Rt = "GetBattleRewardCase", Pool = new() { 401 } });

        RewardResolution.AcquireBattleReward(run, new List<long> { 0 });

        Assert.Equal(expected, run.Cost);
    }

    [Theory]
    [InlineData(100, 200 + 150)]
    [InlineData(200, 200 + 250)]
    [InlineData(303, 200 + 700)]
    [InlineData(109, 200)] // a second starting gift, but no Cost
    public void StartCost_ComesFromTheStoredPick(long buff, long expected)
    {
        var run = StartedRun(buff);

        StartRunRules.AcquireStartAndCreateThemePool(run, 0, new List<long> { 9001 }, false);

        Assert.Equal(expected, run.Cost);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(200, 13)]
    [InlineData(5000, 100)]
    public void SuperShopPercent_MoonStarShopAddsOnTheBaseOdds(long usedCost, long expectedWithoutBuff)
    {
        Assert.Equal(expectedWithoutBuff, MapGenerator.SuperShopPercent(usedCost));
        Assert.Equal(Math.Min(100, expectedWithoutBuff + 25),
            MapGenerator.SuperShopPercent(usedCost, StartBuffEffects.SuperShopBonusPercent(RunWith(305))));
        Assert.Equal(10, StartBuffEffects.SuperShopBonusPercent(RunWith(205)));
        Assert.Equal(0, StartBuffEffects.SuperShopBonusPercent(RunWith(105)));
    }

    // ---- shop ----

    private static Run ShopRun(params long[] buffs)
    {
        var run = RunWith(buffs);
        run.Floor.Nodes.Add(new MapNode { F = 0, S = 5, Nid = 500, E = 10, Eid = 0 });
        run.Party.Add(new PartyUnit { PersonalityId = 10101, CurrentHp = 10000 });
        return run;
    }

    private static void EnterShop(Run run) =>
        MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 5, nid = 500 });

    [Theory]
    [InlineData(new long[] { }, 4)]
    [InlineData(new long[] { 100 }, 5)]
    [InlineData(new long[] { 100, 304 }, 6)]
    public void ShopGiftSlots_GainTheBuffsSlots(long[] buffs, int expected)
    {
        var run = ShopRun(buffs);
        EnterShop(run);

        Assert.Equal(expected, SharedRules.ShopGiftCount(run));
        Assert.Equal(expected, run.Shop.Slots.Count(s => s.T == "eg"));
    }

    [Fact]
    public void StarOfTheBeginning_FreeRefresh_ChargesNothingOncePerShop()
    {
        var run = ShopRun(100);
        run.Cost = 1000;
        EnterShop(run);
        Assert.Equal(1, run.Shop.Fre); // fre = free refreshes left this visit

        ShopRules.RefreshShop(run, "None");
        Assert.Equal((1000L, 0L, 0L), (run.Cost, run.Shop.Fre, run.Shop.Rc));

        ShopRules.RefreshShop(run, "None");
        Assert.Equal((985L, 0L, 1L), (run.Cost, run.Shop.Fre, run.Shop.Rc)); // first paid one: 15

        EnterShop(run); // a new visit restores the allowance
        Assert.Equal(1, run.Shop.Fre);
    }

    [Fact]
    public void StarOfTheBeginningPlusPlus_TwoFreeRefreshes_NotForKeywordRefreshes()
    {
        var run = ShopRun(300);
        run.Cost = 1000;
        EnterShop(run);
        Assert.Equal((2L, 0L), (run.Shop.Fre, run.Shop.Fkre));

        ShopRules.RefreshShop(run, "Burst");
        Assert.Equal(1000 - 15 - 75, run.Cost); // keyword refreshes are not covered
        var afterKeyword = run.Cost;

        ShopRules.RefreshShop(run, "None");
        ShopRules.RefreshShop(run, "None");
        Assert.Equal(afterKeyword, run.Cost);
        Assert.Equal(0, run.Shop.Fre);
    }

    [Fact]
    public void MoonStarShop_FreeKeywordRefresh()
    {
        var run = ShopRun(105);
        run.Cost = 1000;
        EnterShop(run);
        Assert.Equal((0L, 1L), (run.Shop.Fre, run.Shop.Fkre));

        ShopRules.RefreshShop(run, "Burst");
        Assert.Equal((1000L, 0L, 0L), (run.Cost, run.Shop.Fkre, run.Shop.Rc));

        ShopRules.RefreshShop(run, "Burst");
        Assert.Equal(1000 - 15 - 75, run.Cost);
    }

    [Fact]
    public void CumulatingStarcloud_SaleBonus_OncePerShop()
    {
        var run = ShopRun(201); // +75%
        EnterShop(run);
        run.Gifts.Items.Add(new EgoGift { Id = 9001 }); // price 198 -> sells for 99
        run.Gifts.Items.Add(new EgoGift { Id = 9009 }); // price 203 -> sells for 101

        ShopRules.SellEgoGift(run, 9001);
        Assert.Equal(99 * 175 / 100, run.Cost);
        Assert.Equal(1, run.Shop.Aesp);

        ShopRules.SellEgoGift(run, 9009);
        Assert.Equal(99 * 175 / 100 + 101, run.Cost);
    }

    [Fact]
    public void BinaryStarShopPlus_ShopEntryPaysTheOwnedTierSum_UpToTheMax()
    {
        var run = ShopRun(204); // max 75
        run.Gifts.Items.Add(new EgoGift { Id = 9001 });   // Tier 2
        run.Gifts.Items.Add(new EgoGift { Id = 9088 });   // Tier 4
        run.Gifts.Items.Add(new EgoGift { Id = 995014 }); // Adversity: no tier

        EnterShop(run);
        Assert.Equal(6, run.Cost);

        // Entering the same shop node again is not a new visit.
        EnterShop(run);
        Assert.Equal(6, run.Cost);

        var rich = ShopRun(204);
        for (var i = 0; i < 40; i++) rich.Gifts.Items.Add(new EgoGift { Id = 9991 + i % 3 });
        EnterShop(rich);
        Assert.Equal(75, rich.Cost);
    }

    [Theory]
    [InlineData(108, 0, 1, 3)]
    [InlineData(208, 0, 1, 3)]
    [InlineData(208, 5, 2, 3)]
    [InlineData(308, 4, 2, 3)]
    [InlineData(308, 5, 3, 4)]
    public void ChanceComet_ShopEntryVestige(long buff, long floor, long minTier, long maxTier)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var run = RunWith(buff);
            run.Floor.Nodes.Add(new MapNode { F = floor, S = 5, Nid = 500, E = 10 });
            MapNodeRules.EnterNode(run, new Currentnode { f = floor, s = 5, nid = 500 });

            var vestige = Assert.Single(run.Gifts.Items).Id;
            Assert.InRange(vestige, 9990 + minTier, 9990 + maxTier);
        }
    }

    // ---- gifts on floors ----

    [Theory]
    [InlineData(105, 1)]
    [InlineData(205, 2)]
    [InlineData(305, 3)]
    public void MoonStarShop_Tier1GiftsPopUpOnFloor1(long buff, int count)
    {
        var run = StartedRun(buff);

        // Nothing at the starting-gift step: the gifts come with floor 1.
        StartRunRules.AcquireStartAndCreateThemePool(run, 0, new List<long> { 9001 }, false);
        Assert.Equal(new long[] { 9001 }, run.Gifts.Items.Select(g => g.Id));
        Assert.Empty(run.RewardEvents);

        StartBuffEffects.OnEnterFloor(run, 0);

        var popup = Assert.Single(run.RewardEvents);
        Assert.Equal(("GetEgogift", (long)count, (long)count), (popup.Rt, popup.Se, popup.Sh));
        Assert.All(popup.Pool, id => Assert.Equal(1, Tier(id)));
        Assert.Equal(count, popup.Pool.Distinct().Count());
        Assert.Equal(new long[] { 9001 }, run.Gifts.Items.Select(g => g.Id)); // not taken yet

        RewardResolution.AcquireReward(run, Enumerable.Range(0, count).Select(i => (long)i).ToList());
        Assert.Equal(popup.Pool, run.Gifts.Items.Skip(1).Select(g => g.Id));
        Assert.Empty(run.RewardEvents);
    }

    [Fact]
    public void FloorOnePopups_PerfectedPossibilityComesBeforeMoonStarShop()
    {
        var run = StartedRun(105, 109);

        StartRunRules.AcquireStartAndCreateThemePool(run, 0, new List<long> { 9001, 9009 }, false);
        StartBuffEffects.OnEnterFloor(run, 0);

        Assert.Equal(2, run.RewardEvents.Count);
        Assert.All(run.RewardEvents[0].Pool, id => Assert.Equal(3, Tier(id)));
        Assert.All(run.RewardEvents[1].Pool, id => Assert.Equal(1, Tier(id)));
    }

    [Theory]
    [InlineData(109, new long[] { 9991 }, 1)]
    [InlineData(209, new long[] { 9991, 9992 }, 2)]
    [InlineData(309, new long[] { 9991, 9992, 9993 }, 3)]
    public void PerfectedPossibility_VestigesAndOneTier3KeywordPopup(long buff, long[] vestiges, int picks)
    {
        var run = StartedRun(buff);

        // Set 0 is the Combustion starting set.
        StartRunRules.AcquireStartAndCreateThemePool(run, 0, new List<long> { 9001, 9009 }, false);

        Assert.Equal("Combustion", run.StartKeyword);
        Assert.Equal(vestiges, run.Gifts.Items.Where(g => g.Id >= 9991 && g.Id <= 9995).Select(g => g.Id));

        // One popup offering the Keyword's Tier 3 gifts, `picks` of which are taken.
        var popup = Assert.Single(run.RewardEvents);
        Assert.Equal("GetEgogift", popup.Rt);
        Assert.Equal(picks, popup.Se);
        Assert.Equal(popup.Pool.Count, popup.Sh);
        Assert.True(popup.Pool.Count >= picks);
        Assert.Equal(popup.Pool.Count, popup.Pool.Distinct().Count());
        Assert.All(popup.Pool, id =>
        {
            Assert.Equal(3, Tier(id));
            Assert.Equal("Combustion", MdEgoData.GetById(id)!.keyword);
        });

        var wanted = Enumerable.Range(0, picks).Select(i => (long)i).ToList();
        RewardResolution.AcquireReward(run, wanted);
        Assert.All(wanted, i => Assert.Contains(run.Gifts.Items, g => g.Id == popup.Pool[(int)i]));
        Assert.Empty(run.RewardEvents);
    }

    [Fact]
    public void AcquireReward_TakesNoMoreThanThePopupsPicks()
    {
        var run = new Run();
        run.RewardEvents.Add(new RewardEvent { Rt = "GetEgogift", Se = 1, Sh = 3, Pool = new() { 9001, 9009, 9103 } });

        var granted = RewardResolution.AcquireReward(run, new List<long> { 2, 0 });

        Assert.Equal(new long[] { 9103 }, granted.Select(g => g.Id));
    }

    [Theory]
    [InlineData(108, 2, 3, false)]
    [InlineData(108, 4, 0, false)]
    [InlineData(208, 2, 3, true)]
    [InlineData(208, 4, 3, true)]
    [InlineData(208, 6, 0, true)]
    [InlineData(308, 6, 4, true)]
    [InlineData(308, 3, 0, true)]
    public void ChanceComet_FloorGiftsPopUpToBeTaken(long buff, long floor, long tier, bool keyword)
    {
        var run = RunWith(buff);
        run.StartKeyword = "Laceration";

        var offered = StartBuffEffects.OnEnterFloor(run, floor);

        Assert.Empty(run.Gifts.Items); // nothing lands until the popup is taken
        if (tier == 0)
        {
            Assert.Empty(offered);
            Assert.Empty(run.RewardEvents);
            return;
        }
        var id = Assert.Single(offered);
        Assert.Equal(tier, Tier(id));
        if (keyword) Assert.Equal("Laceration", MdEgoData.GetById(id)!.keyword);
        var popup = Assert.Single(run.RewardEvents);
        Assert.Equal(("GetEgogift", 1L, 1L), (popup.Rt, popup.Se, popup.Sh));
        Assert.Equal(new List<long> { id }, popup.Pool);

        RewardResolution.AcquireReward(run, new List<long> { 0 });
        Assert.Equal(id, Assert.Single(run.Gifts.Items).Id);
    }

    // ---- fusion ----

    [Theory]
    [InlineData(108, 9088, 2)] // Tier 4 result, 2 Tiers lower
    [InlineData(308, 9088, 3)] // ++: 1 Tier lower
    public void ChanceComet_FusionBonus_OncePerShop(long buff, long resultGift, long bonusTier)
    {
        var run = RunWith(buff);

        var bonus = StartBuffEffects.OnCombine(run, resultGift);

        Assert.NotNull(bonus);
        Assert.Equal(bonusTier, Tier(bonus!.Id));
        Assert.Equal(1, run.Shop.Aec);
        Assert.Null(StartBuffEffects.OnCombine(run, resultGift));
    }

    [Fact]
    public void ChanceComet_FusionBonus_NeedsATier3Result()
    {
        Assert.Null(StartBuffEffects.OnCombine(RunWith(108), 9001)); // Tier 2
    }

    [Fact]
    public void Combine_ChanceComet_AppendsTheBonusAfterTheResult()
    {
        var run = RunWith(108);
        run.Gifts.Items.Add(new EgoGift { Id = 9003 });
        run.Gifts.Items.Add(new EgoGift { Id = 9053 });
        run.Gifts.Items.Add(new EgoGift { Id = 9157 });

        var result = FusionRules.Combine(run, new() { 9003, 9053, 9157 }, "Sinking", 0);

        Assert.Equal(9088, result.Id);
        Assert.Equal(2, run.Gifts.Items.Count);
        Assert.Same(result, run.Gifts.Items[0]);
        Assert.Equal(2, Tier(run.Gifts.Items[1].Id));
    }
}
