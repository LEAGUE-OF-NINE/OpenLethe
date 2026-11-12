using System.Text.Json.Nodes;
using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.MirrorDungeon.Rules;
using OpenLethe.Server.Wire;

namespace OpenLethe.Tests;

// Component tests for the Task 7 domain primitive: RewardEngine.ResolveNodeExit, ported from
// Handlers/MirrorDungeonMap.cs ExitMirrorDungeonMapNode (:318-564). Structure/behavior only - the
// RNG pool CONTENTS (GetBattleRewardCase.pool, GetEgogiftWithEnemyBuf.pool_v2/pool_v3) are masked
// in the replay and NOT asserted here; the byte-green 407-replay is the golden-value oracle. These
// tests instead lock in the deterministic wiring (which real static-data ids feed which branch,
// grant/no-grant rules, RewardEvents order) using REAL static-data event/reward ids so a
// branch-translation bug shows up here before the (slower) replay.
public class MdRulesRewardEngineTests
{
    private static Run RunWithNode(long nid, long e, long eid, long f = 0)
    {
        var run = new Run();
        run.Floor.Nodes.Add(new MapNode { Nid = nid, E = e, Eid = eid, F = f });
        return run;
    }

    private static RewardEngine.NodeExitResult Exit(Run run, long nid, List<Dungeonunitlist1>? dul = null) =>
        RewardEngine.ResolveNodeExit(
            run,
            new Currentnode { f = 0, s = 0, nid = nid },
            dul ?? new List<Dungeonunitlist1>(),
            noderesult: 0,
            choiceEventData: new ChoiceEventData(),
            isUpdatedEgoSkillStock: false,
            egoSkillStockList: new List<EgoSkillStock>(),
            abnormalityLogs: new List<JsonNode>());

    [Fact]
    public void ResolveNodeExit_RecordsClearedNodeInPnids()
    {
        var run = RunWithNode(nid: 100, e: 1, eid: 0);

        Exit(run, 100);

        Assert.Contains(100, run.Pnids);
    }

    [Fact]
    public void ResolveNodeExit_UpdatesFloorCurrentPositionToExitedNode()
    {
        var run = RunWithNode(nid: 200, e: 1, eid: 0, f: 2);

        RewardEngine.ResolveNodeExit(
            run, new Currentnode { f = 2, s = 1, nid = 200 }, new List<Dungeonunitlist1>(), 0,
            new ChoiceEventData(), false, new List<EgoSkillStock>(), new List<JsonNode>());

        Assert.Equal(2, run.Floor.Current.F);
        Assert.Equal(1, run.Floor.Current.S);
        Assert.Equal(200, run.Floor.Current.Nid);
    }

    [Fact]
    public void ResolveNodeExit_MergesIncomingPartyPreservingUpgradeIndices()
    {
        var run = RunWithNode(nid: 100, e: 1, eid: 0);
        run.Party.Add(new PartyUnit { PersonalityId = 9001, UpgradeIndices = new List<long> { 3, 4 } });

        var incoming = new List<Dungeonunitlist1>
        {
            new() { pid = 9001, ch = 5000, upidx = new List<long>(), es = new List<Egos> { new() { id = 1, g = 7, idx = 0 } } },
        };
        Exit(run, 100, incoming);

        var merged = Assert.Single(run.Party);
        Assert.Equal(9001, merged.PersonalityId);
        Assert.Equal(5000, merged.CurrentHp);
        Assert.Equal(new List<long> { 3, 4 }, merged.UpgradeIndices); // preserved from prior, not the incoming []
        Assert.Equal(-1, merged.Pord);
        Assert.All(merged.EgoSkills, e => Assert.Equal(0, e.G)); // per-ego gauge reset
    }

    [Fact]
    public void ResolveNodeExit_AlwaysResetsShopSlots()
    {
        var run = RunWithNode(nid: 100, e: 1, eid: 0);
        run.Shop.Slots.Add(new ShopSlotState { T = "eg", Id = 1, S = 1 });

        Exit(run, 100);

        Assert.Empty(run.Shop.Slots);
    }

    [Fact]
    public void ResolveNodeExit_EventNode_EmptyChain_RewardEventsStayEmptyButPeidsRecorded()
    {
        // eid 0 resolves to no static event (MdEventData.GetById returns null) - the chain
        // replay pushes no gifts, so RewardEvents (reset to [] every exit) stays empty. Peids
        // still records the node's own eid regardless of whether the chain granted anything.
        var run = RunWithNode(nid: 100, e: 3, eid: 0);

        Exit(run, 100);

        Assert.Empty(run.RewardEvents);
        Assert.Contains(0L, run.Peids);
    }

    [Fact]
    public void ResolveNodeExit_EventNode_RealChain_GrantsBothGiftsFromOneOptionEntry()
    {
        // Real static data: event 901021, option index 1 grants TWO
        // GetConfirmedEgogiftOnWinBattle gifts (9021, 9134) from a single resultList entry - the
        // exact multi-gift-per-entry case the handler's doc comment calls out (fixture nid
        // 202/20100/40301). Deterministic (single Prob_1 entry, no weighted roll).
        var run = RunWithNode(nid: 500, e: 3, eid: 901021);
        run.ChoiceEvents.Add(new ChoiceEvent { Sl = new List<long> { 1 }, Cs = 0 });

        Exit(run, 500);

        Assert.Equal(new List<long> { 9021, 9134 }, run.Gifts.Items.Select(g => g.Id).ToList());
        Assert.Equal(2, run.RewardEvents.Count);
        Assert.All(run.RewardEvents, e => Assert.Equal("GetConfirmedEgogiftOnWinBattle", e.Rt));
        Assert.Equal(new List<long> { 9021 }, run.RewardEvents[0].Pool);
        Assert.Equal(new List<long> { 9134 }, run.RewardEvents[1].Pool);
    }

    [Fact]
    public void ResolveNodeExit_EventNode_AlreadyOwnedGift_SkipsRawAdd()
    {
        // Same chain as above, but 9021 is already owned - the e==3 branch's grant is a RAW
        // owned-check-then-add (NOT SharedRules.GrantEgoGift's Vestige rule): an already-owned
        // gift is simply skipped, not converted to a super/Vestige gift.
        var run = RunWithNode(nid: 500, e: 3, eid: 901021);
        run.ChoiceEvents.Add(new ChoiceEvent { Sl = new List<long> { 1 }, Cs = 0 });
        run.Gifts.Items.Add(new EgoGift { Id = 9021 });

        Exit(run, 500);

        Assert.Equal(new List<long> { 9021, 9134 }, run.Gifts.Items.Select(g => g.Id).ToList()); // no duplicate, no Vestige
        Assert.Single(run.RewardEvents); // only the 9134 grant queues a RewardEvent
        Assert.Equal(new List<long> { 9134 }, run.RewardEvents[0].Pool);
    }

    [Fact]
    public void ResolveNodeExit_BossClear_GrantsLeadingConfirmedGiftAndDrivesEgmlos()
    {
        // Real static data: boss stage 2067134 (mirrordungeon-battle5-0.json) rewardList is a
        // single EGO_GIFT 993005 (+3 egmlos per Effects.HiddenGiftLevelBump).
        var run = RunWithNode(nid: 600, e: 6, eid: 2067134, f: 5);
        run.ThemeFloors.Add(new ThemeFloor { Idx = 0, F = 5, Tfid = 1, Egs = new List<long> { 111, 222 } });
        run.Party.Add(new PartyUnit { PersonalityId = 1 });

        Exit(run, 600);

        Assert.Contains(993005L, run.Gifts.Items.Select(g => g.Id));
        Assert.Equal(3, run.LevelOffsets.Egmlos); // Effects.HiddenGiftLevelBump(993005)

        // RewardEvents order: [ leadingConfirmed, GetBattleRewardCase, GetEgogiftWithEnemyBuf ]
        // (no GetConstraints below floor 10).
        Assert.Equal(3, run.RewardEvents.Count);
        Assert.Equal("GetConfirmedEgogiftOnWinBattle", run.RewardEvents[0].Rt);
        Assert.Equal(new List<long> { 993005 }, run.RewardEvents[0].Pool);
        Assert.Equal("GetBattleRewardCase", run.RewardEvents[1].Rt);
        Assert.Equal("GetEgogiftWithEnemyBuf", run.RewardEvents[2].Rt);
        // ledger task-11b invariant: GetEgogiftWithEnemyBuf.pool == tfs[^1].egs.
        Assert.Equal(run.ThemeFloors[^1].Egs, run.RewardEvents[2].Pool);

        // Per-unit level cap wiring: mlos == sbmlos + egmlos.
        Assert.All(run.Party, u => Assert.Equal(run.LevelOffsets.Sbmlos + run.LevelOffsets.Egmlos, u.Mlos));

        // Cleared-floor difficulty record appended exactly once.
        Assert.Single(run.ConstraintScores);
    }

    [Theory]
    [InlineData(0, 1)] // normal pack: pick 1 clear-bonus gift
    [InlineData(1, 2)] // hard pack: pick 2 (capture-verified)
    public void ResolveNodeExit_BossClear_ClearBonusPickCountFollowsDifficulty(long idx, long picks)
    {
        var run = RunWithNode(nid: 600, e: 6, eid: 2067134, f: 1);
        run.Idx = idx;
        run.ThemeFloors.Add(new ThemeFloor { Idx = 1, F = 1, Tfid = 1001, Egs = new List<long> { 111, 222, 333 } });

        Exit(run, 600);

        var popup = Assert.Single(run.RewardEvents, e => e.Rt == "GetEgogiftWithEnemyBuf");
        Assert.Equal(picks, popup.Se);
    }

    [Fact]
    public void ResolveNodeExit_BossClear_AlreadyOwnedBossGift_LeadingConfirmedPoolIsEmpty()
    {
        var run = RunWithNode(nid: 600, e: 6, eid: 2067134, f: 5);
        run.Gifts.Items.Add(new EgoGift { Id = 993005 }); // already owned before this exit

        Exit(run, 600);

        // sh stays 1 even though the survivors pool is empty (per the ledger's documented rule).
        Assert.Equal("GetConfirmedEgogiftOnWinBattle", run.RewardEvents[0].Rt);
        Assert.Empty(run.RewardEvents[0].Pool);
        Assert.Equal(1, run.RewardEvents[0].Sh);
        // No duplicate grant.
        Assert.Single(run.Gifts.Items);
    }

    [Fact]
    public void ResolveNodeExit_BossClear_ExpiresFloorScopedHiddenGifts()
    {
        var run = RunWithNode(nid: 600, e: 6, eid: 0, f: 0); // eid 0 -> no boss rewardList
        run.Gifts.Items.Add(new EgoGift { Id = 991002 }); // floor-scoped hidden gift
        run.Gifts.Items.Add(new EgoGift { Id = 9040 });   // ordinary gift, must survive

        Exit(run, 600);

        Assert.DoesNotContain(run.Gifts.Items, g => g.Id == 991002);
        Assert.Contains(run.Gifts.Items, g => g.Id == 9040);
    }

    [Fact]
    public void ResolveNodeExit_HardAbnoBattle_GrantsRewardGifts_OwnedOneBecomesVestige()
    {
        // Real static data: abno stage 2061141 (mirrordungeon-abbattle5-0.json) rewardList has
        // TWO EGO_GIFTs [9014, 9103]. e==14 grants through SharedRules.GrantEgoGift: the
        // already-owned 9014 turns into its tier's Vestige instead of a second copy.
        var run = RunWithNode(nid: 700, e: 14, eid: 2061141);
        run.Gifts.Items.Add(new EgoGift { Id = 9014 }); // pre-owned
        var vestige = StartBuffEffects.VestigeOfTier(OpenLethe.Server.MdEgoData.DetermineEgoTier(9014)!.Value);

        Exit(run, 700);

        Assert.Equal(new List<long> { 9014, vestige, 9103 }, run.Gifts.Items.Select(g => g.Id).ToList());
        Assert.Equal(9014, run.Gifts.Items[1].Oid);
        Assert.Equal(2, run.RewardEvents.Count);
        Assert.All(run.RewardEvents, e => Assert.Equal("GetConfirmedEgogiftOnWinBattle", e.Rt));
        Assert.Equal(new List<long> { 9014 }, run.RewardEvents[0].Pool);
        Assert.Equal(new List<long> { 9103 }, run.RewardEvents[1].Pool);
    }

    [Fact]
    public void ResolveNodeExit_ShopNode_NoRewardEventsQueued()
    {
        var run = RunWithNode(nid: 100, e: 10, eid: 0);

        Exit(run, 100);

        Assert.Empty(run.RewardEvents);
    }

    [Fact]
    public void ResolveNodeExit_EchoesAbnormalityLogsSortedById()
    {
        var run = RunWithNode(nid: 100, e: 1, eid: 0);
        var log2 = new JsonObject { ["id"] = 200L };
        var log1 = new JsonObject { ["id"] = 100L };

        var result = RewardEngine.ResolveNodeExit(
            run, new Currentnode { f = 0, s = 0, nid = 100 }, new List<Dungeonunitlist1>(), 0,
            new ChoiceEventData(), false, new List<EgoSkillStock>(),
            new List<JsonNode> { log2, log1 });

        Assert.Equal(2, result.AbnormalityLogs.Count);
        Assert.Equal(100L, ((JsonObject)result.AbnormalityLogs[0])["id"]!.GetValue<long>());
        Assert.Equal(200L, ((JsonObject)result.AbnormalityLogs[1])["id"]!.GetValue<long>());
    }

    [Fact]
    public void ResolveNodeExit_AddsVictoryCostForNodeKind()
    {
        var run = RunWithNode(nid: 100, e: 1, eid: 0); // e==1 battle, hard -> 85
        run.Idx = 1;
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 1202 });
        run.Cost = 10;

        Exit(run, 100);

        Assert.Equal(95, run.Cost);
    }

    [Fact]
    public void ResolveNodeExit_NormalRun_PaysNormalTable()
    {
        var run = RunWithNode(nid: 100, e: 5, eid: 0); // e==5 abno battle, normal -> 100
        run.Idx = 0;
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 1001 });
        run.Cost = 10;

        Exit(run, 100);

        Assert.Equal(110, run.Cost);
    }

    [Theory]
    [InlineData(1, 300)]  // floor 2 boss, hard
    [InlineData(7, 500)]  // floor 8 boss, hard: past the old 4-floor table, used to pay 0
    [InlineData(12, 500)] // floor 13 (extreme)
    public void ResolveNodeExit_BossClear_PaysFloorWeightedBossCost(long f, long expected)
    {
        var run = RunWithNode(nid: 600, e: 6, eid: 2067134, f: f);
        run.Idx = f >= 10 ? 3 : f >= 5 ? 2 : 1;
        for (long i = 0; i <= f; i++)
            run.ThemeFloors.Add(new ThemeFloor { F = i, Idx = i / 5 + 1, Tfid = 1202, Egs = new List<long> { 111 } });
        run.Cost = 0;

        RewardEngine.ResolveNodeExit(
            run, new Currentnode { f = f, s = 0, nid = 600 }, new List<Dungeonunitlist1>(), 0,
            new ChoiceEventData(), false, new List<EgoSkillStock>(), new List<JsonNode>());

        Assert.Equal(expected, run.Cost);
    }

    [Fact]
    public void ResolveNodeExit_HiddenBoss_GrantsExclusiveGiftAndPays1000()
    {
        // A hidden node (e==15) against 9000007, whose exclusive gift is 9256.
        var run = RunWithNode(nid: 100102, e: 15, eid: 9000007, f: 10);
        run.Cost = 575;

        Exit(run, 100102);

        Assert.Contains(run.Gifts.Items, g => g.Id == 9256);
        Assert.Equal(575 + 1000, run.Cost);
        Assert.Equal("GetBattleRewardCase", Assert.Single(run.RewardEvents).Rt);
    }

    private static List<Dungeonunitlist1> Roster(params (long pid, long ch, long cm)[] units) =>
        units.Select(u => new Dungeonunitlist1 { pid = u.pid, ch = u.ch, cm = u.cm }).ToList();

    [Fact]
    public void ResolveNodeExit_HiddenBoss_RevivesTheParty()
    {
        // md-extreme-run-2 seq339: the dead come back at full hp with cm cleared, the living are
        // healed and keep their cm.
        var run = RunWithNode(nid: 100102, e: 15, eid: 9000007, f: 10);

        Exit(run, 100102, Roster((10306, 0, 45), (11115, 2153, 30)));

        Assert.Equal(new long[] { 10000, 10000 }, run.Party.Select(u => u.CurrentHp));
        Assert.Equal(new long[] { 0, 30 }, run.Party.Select(u => u.Cm));
    }

    [Fact]
    public void ResolveNodeExit_HiddenBoss_SpiderwebWithBladeRyoshu_KeepsOthersDead()
    {
        var run = RunWithNode(nid: 100102, e: 15, eid: 9000007, f: 10);
        run.Gifts.Items.Add(new EgoGift { Id = 9273 });

        Exit(run, 100102, Roster((10306, 0, 45), (10415, 0, 20), (11115, 2153, 30)));

        Assert.Equal(new long[] { 0, 10000, 10000 }, run.Party.Select(u => u.CurrentHp));
    }

    [Fact]
    public void ResolveNodeExit_HiddenBoss_SpiderwebWithoutBladeRyoshu_StillRevives()
    {
        var run = RunWithNode(nid: 100102, e: 15, eid: 9000007, f: 10);
        run.Gifts.Items.Add(new EgoGift { Id = 9273 });

        Exit(run, 100102, Roster((10306, 0, 45), (11115, 2153, 30)));

        Assert.All(run.Party, u => Assert.Equal(10000, u.CurrentHp));
    }

    [Fact]
    public void ResolveNodeExit_BossClear_MilepostOfSurvivalPaysPerParticipant()
    {
        // Stage 2067134 deploys 7. Eight sinners were standing, so 7 took part: 80 + 7*5 = 115 on
        // top of the floor-2 hard boss's 300, and Golden Urn's +20% does not scale it.
        var run = RunWithNode(nid: 600, e: 6, eid: 2067134, f: 1);
        run.Idx = 1;
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 1202, Egs = new List<long> { 111 } });
        run.ThemeFloors.Add(new ThemeFloor { F = 1, Idx = 1, Tfid = 1202, Egs = new List<long> { 111 } });
        run.Gifts.Items.Add(new EgoGift { Id = 9080 });
        run.Gifts.Items.Add(new EgoGift { Id = 9079 });
        run.Party.AddRange(Enumerable.Range(0, 12).Select(i => new PartyUnit { PersonalityId = 100 + i, CurrentHp = i < 8 ? 10000 : 0 }));
        run.Cost = 0;

        RewardEngine.ResolveNodeExit(
            run, new Currentnode { f = 1, s = 0, nid = 600 }, new List<Dungeonunitlist1>(), 0,
            new ChoiceEventData(), false, new List<EgoSkillStock>(), new List<JsonNode>());

        Assert.Equal(300 * 120 / 100 + 115, run.Cost);
    }

    [Fact]
    public void ResolveNodeExit_BossClear_MilepostOfSurvivalCountsOnlyStandingSinners()
    {
        var run = RunWithNode(nid: 600, e: 6, eid: 2067134, f: 1);
        run.Idx = 1;
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 1202, Egs = new List<long> { 111 } });
        run.ThemeFloors.Add(new ThemeFloor { F = 1, Idx = 1, Tfid = 1202, Egs = new List<long> { 111 } });
        run.Gifts.Items.Add(new EgoGift { Id = 9080 });
        run.Party.AddRange(Enumerable.Range(0, 12).Select(i => new PartyUnit { PersonalityId = 100 + i, CurrentHp = i < 3 ? 10000 : 0 }));
        run.Cost = 0;

        RewardEngine.ResolveNodeExit(
            run, new Currentnode { f = 1, s = 0, nid = 600 }, new List<Dungeonunitlist1>(), 0,
            new ChoiceEventData(), false, new List<EgoSkillStock>(), new List<JsonNode>());

        Assert.Equal(300 + 80 + 3 * 5, run.Cost);
    }
}
