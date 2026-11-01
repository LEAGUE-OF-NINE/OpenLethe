using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.MirrorDungeon.Rules;

namespace OpenLethe.Tests;

// Component tests for the Task 9 reward-RESOLUTION group: RewardResolution (Run-mutating).
// Mirrors the wire-typed handler bodies these replace (Handlers/MirrorDungeonShop.cs
// AcquireRewardEgoGiftsMirrorDungeon/RejectRewardEgoGiftsMirrorDungeon, Handlers/
// MirrorDungeonRewards.cs AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon/
// AcquireMirrorDungeonBattleReward). Structure/behavior, not golden RNG - the grant/consume
// lifecycle and the Vestige-via-GrantEgoGift rule are asserted; RNG pool contents are not.
public class MdRulesRewardResolutionTests
{
    [Fact]
    public void AcquireReward_GrantsPickedGift_AndConsumesOnlyGetEgogift_KeepingSiblings()
    {
        var run = new Run();
        var getEgogift = new RewardEvent { Rt = "GetEgogift", Se = 1, Sh = 1, Pool = new() { 9004, 9005 } };
        var sibling = new RewardEvent { Rt = "GetBattleRewardCase", Se = 1, Sh = 2, Pool = new() { 1, 2 } };
        run.RewardEvents.Add(getEgogift);
        run.RewardEvents.Add(sibling);

        var granted = RewardResolution.AcquireReward(run, new() { 1 }); // pick index 1 -> 9005

        Assert.Single(granted);
        Assert.Equal(9005, granted[0].Id);
        Assert.Contains(run.Gifts.Items, g => g.Id == 9005);
        Assert.Single(run.RewardEvents);
        Assert.Same(sibling, run.RewardEvents[0]); // sibling event untouched
    }

    [Fact]
    public void AcquireReward_AlreadyOwnedGift_GrantsViaVestigeRule()
    {
        var run = new Run();
        run.Gifts.Items.Add(new EgoGift { Id = 9053 }); // TIER_3
        run.RewardEvents.Add(new RewardEvent { Rt = "GetEgogift", Se = 1, Sh = 1, Pool = new() { 9053 } });

        var granted = RewardResolution.AcquireReward(run, new() { 0 });

        Assert.Equal(9993, granted[0].Id); // super/Vestige id, not a raw duplicate add
        Assert.Equal(9053, granted[0].Oid);
    }

    [Fact]
    public void AcquireReward_DefaultsToIndexZero_WhenSelectIndexListEmpty()
    {
        var run = new Run();
        run.RewardEvents.Add(new RewardEvent { Rt = "GetEgogift", Se = 1, Sh = 1, Pool = new() { 9010 } });

        var granted = RewardResolution.AcquireReward(run, new());

        Assert.Equal(9010, granted[0].Id);
    }

    [Fact]
    public void RejectReward_ClearsAllRewardEvents()
    {
        var run = new Run();
        run.RewardEvents.Add(new RewardEvent { Rt = "GetEgogift", Pool = new() { 9004 } });
        run.RewardEvents.Add(new RewardEvent { Rt = "GetBattleRewardCase", Pool = new() { 1 } });

        RewardResolution.RejectReward(run);

        Assert.Empty(run.RewardEvents);
        Assert.Empty(run.Gifts.Items); // reject grants nothing
    }

    [Fact]
    public void AcquireWithEnemyBuf_GrantsPoolThenPoolV2_Grouped_NotInterleaved()
    {
        var run = new Run();
        run.Floor.Current.F = 0;
        run.RewardEvents.Add(new RewardEvent
        {
            Rt = "GetEgogiftWithEnemyBuf",
            Se = 2,
            Sh = 2,
            Pool = new() { 9004, 9005 },
            PoolV2 = new() { 992201, 992202 },
        });

        var (granted, _) = RewardResolution.AcquireWithEnemyBuf(run, new() { 0, 1 });

        // ALL pool picks first, THEN all pool_v2 picks - grouped order, not interleaved.
        Assert.Equal(new long[] { 9004, 9005, 992201, 992202 }, granted.Select(g => g.Id).ToArray());
        Assert.Equal(new long[] { 9004, 9005, 992201, 992202 }, run.Gifts.Items.Select(g => g.Id).ToArray());
    }

    [Fact]
    public void AcquireWithEnemyBuf_ConsumesOnlyItsPopup_KeepingSiblings()
    {
        var run = new Run();
        run.Floor.Current.F = 0;
        var popup = new RewardEvent { Rt = "GetEgogiftWithEnemyBuf", Se = 1, Sh = 1, Pool = new() { 9004 }, PoolV2 = new() };
        var leading = new RewardEvent { Rt = "GetConfirmedEgogiftOnWinBattle", Pool = new() { 9010 } };
        var trailing = new RewardEvent { Rt = "GetConstraints", Pool = new() { 1 } };
        run.RewardEvents.Add(leading);
        run.RewardEvents.Add(popup);
        run.RewardEvents.Add(trailing);

        RewardResolution.AcquireWithEnemyBuf(run, new() { 0 });

        Assert.Equal(new[] { leading, trailing }, run.RewardEvents);
    }

    [Fact]
    public void AcquireWithEnemyBuf_AppendsExactlyTwoLevelAdders()
    {
        var run = new Run();
        run.Floor.Current.F = 0;
        run.LevelAdders.Add(1); // pre-existing floor's entries

        var (_, levelAdders) = RewardResolution.AcquireWithEnemyBuf(run, new());

        Assert.Equal(3, levelAdders.Count);
        Assert.Same(run.LevelAdders, levelAdders);
    }

    [Fact]
    public void AcquireWithEnemyBuf_RollsNextFloorThemePool_WithoutBumpingTfpsCreatedOrClearingStartPools()
    {
        var run = new Run();
        run.Floor.Current.F = 4;
        run.TfpsCreated = 3;
        run.StartPools.Add(new StartEgoGiftPool { SetId = 1 });

        RewardResolution.AcquireWithEnemyBuf(run, new());

        Assert.Equal(4, run.ThemePools.Count); // 4-theme roll for the next floor
        // Verified against both captures (fixtures seq43/106/191/...): tfpsCreated stays exactly
        // what it was, and seps is never cleared here - unlike MapGenerator.RecreateThemePool
        // (used by RecreateThemeFloorPoolMirrorDungeon), which bumps both. This endpoint does
        // neither; using the shared helper wholesale would regress the byte-green replay.
        Assert.Equal(3, run.TfpsCreated);
        Assert.Single(run.StartPools);
    }

    [Theory]
    [InlineData(new long[] { 0, 1 }, true)]    // +2 +2 = 4: one bonus pick
    [InlineData(new long[] { 0, 2 }, false)]   // +2 +1 = 3: none
    public void AcquireWithEnemyBuf_PicksAddingUpToFourEnemyLevels_QueueOneBonusGiftPick(long[] picks, bool bonus)
    {
        var run = new Run { Idx = 1 };
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 1202 });
        var packPool = new OpenLethe.Server.MirrorDungeon.Data.MdThemePool().ThemeDropPool(1202);
        var trailing = new RewardEvent { Rt = "GetConstraints", Pool = new() { 1 } };
        run.RewardEvents.Add(new RewardEvent
        {
            Rt = "GetEgogiftWithEnemyBuf",
            Se = 2,
            Sh = 3,
            Pool = new() { 9004, 9005, 9006 },
            PoolV2 = new() { 992201, 992202, 992203 },
            PoolV3 = new() { 2, 2, 1 },
        });
        run.RewardEvents.Add(trailing);

        RewardResolution.AcquireWithEnemyBuf(run, picks.ToList());

        if (!bonus)
        {
            Assert.Equal(new[] { trailing }, run.RewardEvents);
            return;
        }
        // Retail appends it after every remaining event: a 1-of-3 GetEgogift from the floor's pack.
        Assert.Equal(2, run.RewardEvents.Count);
        Assert.Same(trailing, run.RewardEvents[0]);
        var extra = run.RewardEvents[1];
        Assert.Equal("GetEgogift", extra.Rt);
        Assert.Equal(1, extra.Se);
        Assert.Equal(3, extra.Sh);
        Assert.Equal(3, extra.Pool.Distinct().Count());
        Assert.All(extra.Pool, id => Assert.Contains(id, packPool));
    }

    [Fact]
    public void AcquireWithEnemyBuf_BonusPickOnACustomPackDrawsFromIt()
    {
        var custom = new OpenLethe.Server.ThemeStatic { id = 990001, egoGiftPool = new() { 9101, 9102, 9103 } };
        var run = new Run { Idx = 1 };
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 990001 });
        run.RewardEvents.Add(new RewardEvent
        {
            Rt = "GetEgogiftWithEnemyBuf", Se = 2, Sh = 2,
            Pool = new() { 9004, 9005 }, PoolV2 = new() { 992201, 992202 }, PoolV3 = new() { 2, 2 },
        });

        RewardResolution.AcquireWithEnemyBuf(run, new() { 0, 1 }, new[] { custom });

        var extra = Assert.Single(run.RewardEvents);
        Assert.Equal(new long[] { 9101, 9102, 9103 }, extra.Pool.OrderBy(id => id).ToArray());
    }

    [Fact]
    public void RejectWithEnemyBuf_ConsumesPopupAddsLevelAddersAndRollsNextFloorPool()
    {
        var run = new Run { Idx = 1 };
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 1202 });
        run.Floor.Current.F = 0;
        var popup = new RewardEvent { Rt = "GetEgogiftWithEnemyBuf", Pool = new() { 9001 }, PoolV2 = new() };
        var trailing = new RewardEvent { Rt = "GetConstraints", Pool = new() { 1 } };
        run.RewardEvents.Add(popup);
        run.RewardEvents.Add(trailing);

        var levelAdders = RewardResolution.RejectWithEnemyBuf(run);

        Assert.Equal(new[] { trailing }, run.RewardEvents);
        Assert.Equal(2, levelAdders.Count);
        Assert.Empty(run.Gifts.Items);
        Assert.Equal(4, run.ThemePools.Count);
        Assert.All(run.ThemePools, t => Assert.Equal(1, t.Idx));
        Assert.DoesNotContain(run.ThemePools, t => t.Tfid == 1202);

        // A repeated skip must not stack another floor's level adders.
        RewardResolution.RejectWithEnemyBuf(run);
        Assert.Equal(2, run.LevelAdders.Count);
    }

    [Fact]
    public void AcquireBattleReward_ConsumesGetBattleRewardCase_KeepingSiblings()
    {
        var run = new Run();
        var card = new RewardEvent { Rt = "GetBattleRewardCase", Se = 1, Sh = 1, Pool = new() { 101 } }; // COST card, 80-120
        var leading = new RewardEvent { Rt = "GetConfirmedEgogiftOnWinBattle", Pool = new() { 9010 } };
        var trailing = new RewardEvent { Rt = "GetEgogiftWithEnemyBuf", Pool = new(), PoolV2 = new() };
        run.RewardEvents.Add(leading);
        run.RewardEvents.Add(card);
        run.RewardEvents.Add(trailing);

        RewardResolution.AcquireBattleReward(run, new() { 0 });

        Assert.Equal(new[] { leading, trailing }, run.RewardEvents);
    }

    // Every pack's exclusive (specificEgoGiftPool) gifts.
    private static HashSet<long> AllExclusives() =>
        new OpenLethe.Server.MirrorDungeon.Data.MdThemePool().pools.Values
            .SelectMany(t => t.specificEgoGiftPool).ToHashSet();

    [Fact]
    public void AcquireBattleReward_GiftCard_DrawsOnlyFromTheCurrentPack()
    {
        // Card 302: EGOGIFT, prob 1, tier 1-2. Pack 1101's exclusives are 9701-9705.
        var packPool = new OpenLethe.Server.MirrorDungeon.Data.MdThemePool().ThemeDropPool(1101);
        var foreign = AllExclusives().Except(packPool).ToHashSet();
        Assert.NotEmpty(foreign);

        for (var i = 0; i < 300; i++)
        {
            var run = new Run();
            run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 1101 });
            run.RewardEvents.Add(new RewardEvent { Rt = "GetBattleRewardCase", Pool = new() { 302 } });

            RewardResolution.AcquireBattleReward(run, new() { 0 });

            var gift = Assert.Single(run.Gifts.Items).Id;
            Assert.Contains(gift, packPool);
            Assert.DoesNotContain(gift, foreign);
        }
    }

    [Fact]
    public void AcquireBattleReward_GiftCard_OnACustomPackDrawsFromItsEgoGiftPool()
    {
        // An uploaded pack whose only tier 1-2 gift is 9101 (TIER_1).
        var custom = new OpenLethe.Server.ThemeStatic { id = 990001, egoGiftPool = new() { 9101 } };
        var run = new Run();
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 990001 });
        run.RewardEvents.Add(new RewardEvent { Rt = "GetBattleRewardCase", Pool = new() { 302 } });

        RewardResolution.AcquireBattleReward(run, new() { 0 }, new[] { custom });

        Assert.Equal(9101, Assert.Single(run.Gifts.Items).Id);
    }

    [Fact]
    public void AcquireBattleReward_CostCard_AddsCostWithinTableRange()
    {
        var run = new Run { Cost = 1000 };
        run.RewardEvents.Add(new RewardEvent { Rt = "GetBattleRewardCase", Pool = new() { 101 } }); // 80-120

        RewardResolution.AcquireBattleReward(run, new() { 0 });

        Assert.InRange(run.Cost, 1080, 1120);
    }

    [Fact]
    public void AcquireBattleReward_EgoStockCard_RaisesExactlyKindStocksByNum()
    {
        var run = new Run();
        run.RewardEvents.Add(new RewardEvent { Rt = "GetBattleRewardCase", Pool = new() { 503 } }); // leastEgoStock kind=2 num=6

        RewardResolution.AcquireBattleReward(run, new() { 0 });

        Assert.Equal(7, run.SkillStocks.Count); // all 7 stock types always present after
        Assert.Equal(12, run.SkillStocks.Sum(s => s.N)); // exactly 2 keys raised by 6
        Assert.Equal(2, run.SkillStocks.Count(s => s.N == 6));
    }

    [Fact]
    public void AcquireBattleReward_TfpsStaysUntouched()
    {
        var run = new Run();
        run.RewardEvents.Add(new RewardEvent { Rt = "GetBattleRewardCase", Pool = new() { 101 } });

        RewardResolution.AcquireBattleReward(run, new() { 0 });

        Assert.Empty(run.ThemePools);
    }

    [Fact]
    public void AcquireBattleReward_OutOfRangeIndex_IsSkipped()
    {
        var run = new Run { Cost = 1000 };
        run.RewardEvents.Add(new RewardEvent { Rt = "GetBattleRewardCase", Pool = new() { 101 } });

        RewardResolution.AcquireBattleReward(run, new() { 99 });

        Assert.Equal(1000, run.Cost); // no card resolved, but the popup is still consumed
        Assert.Empty(run.RewardEvents);
    }

    // Task 10: exit-reward + read endpoints. PreviewExitReward/AcquireExitReward are ports of
    // Handlers/MirrorDungeonShop.cs PreviewMirrorDungeonExitReward/AcquireMirrorDungeonExitReward;
    // ExitMirrorDungeon is a port of Handlers/MirrorDungeonRewards.cs ExitMirrorDungeon. The
    // exit-reward option engine itself (MdExitReward.BuildOptions, MdTheme.cs) stays put and is
    // reused, not re-implemented here - these tests assert the Rules-layer wiring, not the table
    // math (already covered wherever MdExitReward's own behavior is exercised by the replay).

    [Fact]
    public void PreviewExitReward_BuildsExactlyFourOptions_ScaledByIndex()
    {
        var run = new Run();

        var (options, totalConstraintScore) = RewardResolution.PreviewExitReward(run);

        Assert.Equal(4, options.Count);
        Assert.Equal(new long[] { 0, 1, 2, 3 }, options.Select(o => o.chanceConsumption).ToArray());
        // option 1 and option 2 both scale the same unit-bucket elements by their own index -
        // so option 2's element nums are exactly double option 1's (same ids, same order).
        for (var i = 0; i < options[1].rewardList.Count; i++)
        {
            Assert.Equal(options[1].rewardList[i].id, options[2].rewardList[i].id);
            Assert.Equal(options[1].rewardList[i].num * 2, options[2].rewardList[i].num);
        }
        // No known constraint-score formula is wired yet (matches the pre-migration handler,
        // which hardcoded 0 regardless of the run's scinfos) - see MdExitReward.BuildOptions doc.
        Assert.Equal(0, totalConstraintScore);
    }

    [Fact]
    public void AcquireExitReward_SetsEveryPartyUnitIspToOne()
    {
        var run = new Run();
        run.Party.Add(new PartyUnit { PersonalityId = 1, Isp = 0 });
        run.Party.Add(new PartyUnit { PersonalityId = 2, Isp = 0 });

        RewardResolution.AcquireExitReward(run, useEnkephalinModule: false, chanceConsumption: 0);

        Assert.All(run.Party, u => Assert.Equal(1, u.Isp));
    }

    [Fact]
    public void AcquireExitReward_ReturnsOnlyItemElements_ForTheChosenChanceConsumption()
    {
        var run = new Run();

        var granted = RewardResolution.AcquireExitReward(run, useEnkephalinModule: false, chanceConsumption: 3);

        Assert.NotEmpty(granted);
        Assert.All(granted, e => Assert.Equal("ITEM", e.type_));
    }

    [Fact]
    public void AcquireExitReward_UnknownChanceConsumption_GrantsNothing()
    {
        var run = new Run();

        var granted = RewardResolution.AcquireExitReward(run, useEnkephalinModule: false, chanceConsumption: 999);

        Assert.Empty(granted);
    }

    [Fact]
    public void ExitMirrorDungeon_SetsIsEndDungeonFlat()
    {
        var run = new Run { IsEndDungeon = 0 };

        RewardResolution.ExitMirrorDungeon(run);

        Assert.Equal(1, run.IsEndDungeon);
    }
}
