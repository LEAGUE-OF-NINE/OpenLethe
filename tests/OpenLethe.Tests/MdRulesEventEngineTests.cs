using System.Collections.Generic;
using System.Linq;
using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.MirrorDungeon.Rules;
using OpenLethe.Server.Wire;
using Xunit;

namespace OpenLethe.Tests;

// Component tests for the Task 11 event-engine rules: EventEngine (Run-mutating), delegating to
// the shared MdEventManager reader (unchanged, not moved). Uses the same real static-data event
// ids as MirrorDungeonEventsHandlerTests (the pre-existing HTTP-level coverage for this endpoint)
// so the fixtures are known-good, just exercised directly against Run instead of through the
// wire handler + Postgres.
public class MdRulesEventEngineTests
{
    private static Run NewRunWithNode(long nid, long eid)
    {
        var run = new Run();
        run.Floor.Nodes.Add(new MapNode { Nid = nid, Eid = eid });
        run.Floor.Current = new CurrentPosition { Nid = nid };
        run.Party.Add(new PartyUnit { PersonalityId = 7, CurrentHp = 10000, Cm = 0 });
        return run;
    }

    [Fact]
    public void UpdateNode_NormalConfirmedGift_IsQueuedNotSurfacedImmediately()
    {
        // Real static-data: mirror-dungeon-personality-choice-event.json id 90100201,
        // eventResults[0]=SUCCESS -> GetConfirmedEgogift rewardId 9002 (a normal 4-digit gift).
        var run = NewRunWithNode(nid: 101, eid: 90100201);

        EventEngine.UpdateNode(run, new Currentnode { f = 0, s = 1, nid = 101 },
            new ChoiceEventData { sl = new() { 7 }, cs = 0, ri = 0 }, new List<Dungeonunitlist1>());

        // Deferred: the normal gift is pushed by ProcessEvent then stripped back out - it does
        // NOT surface in Gifts until ExitMapNode's e==3 branch realizes it later.
        Assert.DoesNotContain(run.Gifts.Items, g => g.Id == 9002);
        Assert.Single(run.ChoiceEvents);
    }

    [Fact]
    public void UpdateNode_HiddenLevelCapGift_LandsImmediatelyAndBumpsMlos()
    {
        // Real static-data: id 90107402 (COIN_EVENT), cs=0 -> GetConfirmedEgogift 993003
        // (hidden level-cap gift, +2 mlos bump - see Effects.HiddenGiftLevelBump).
        var run = NewRunWithNode(nid: 202, eid: 90107402);
        run.LevelOffsets.Sbmlos = 3;

        EventEngine.UpdateNode(run, new Currentnode { f = 0, s = 1, nid = 202 },
            new ChoiceEventData { sl = new() { 7 }, cs = 0, ri = 0 }, new List<Dungeonunitlist1>());

        Assert.Contains(run.Gifts.Items, g => g.Id == 993003);
        Assert.Equal(2, run.LevelOffsets.Egmlos);
        Assert.All(run.Party, u => Assert.Equal(5, u.Mlos)); // sbmlos 3 + egmlos 2
    }

    [Fact]
    public void UpdateNode_AppendsChoiceEventEchoingRequest()
    {
        var run = NewRunWithNode(nid: 101, eid: 90100201);

        EventEngine.UpdateNode(run, new Currentnode { f = 0, s = 1, nid = 101 },
            new ChoiceEventData { sl = new() { 7 }, cs = 0, ri = 1 }, new List<Dungeonunitlist1>());

        var pce = Assert.Single(run.ChoiceEvents);
        Assert.Equal(new List<long> { 7 }, pce.Sl);
        Assert.Equal(0, pce.Cs);
        Assert.Equal(1, pce.Ri); // echoes the request verbatim, not a fixed sentinel
    }

    [Fact]
    public void UpdateNode_PrevChoiceEventNeiPath_TakesPriorityOverNodeLookup()
    {
        // pce.First().Nei resolves the event id BEFORE the dungeonMap node lookup runs - a node
        // that maps to a different (wrong) eid must be ignored while an open chain exists.
        var run = NewRunWithNode(nid: 999, eid: 111111); // node's own eid is deliberately wrong
        run.ChoiceEvents.Add(new ChoiceEvent { Sl = new() { 0 }, Cs = -1, Ri = 0, Nei = 90100201 });

        EventEngine.UpdateNode(run, new Currentnode { f = 0, s = 0, nid = 999 },
            new ChoiceEventData { sl = new() { 0 }, cs = 0, ri = 0 }, new List<Dungeonunitlist1>());

        Assert.Equal(2, run.ChoiceEvents.Count);
        Assert.Equal(90100201, run.ChoiceEvents[0].Nei);
    }

    [Fact]
    public void UpdateNode_EidUnresolvable_Throws()
    {
        var run = new Run(); // no nodes, no pce chain
        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() =>
            EventEngine.UpdateNode(run, new Currentnode { f = 0, s = 0, nid = 999999 },
                new ChoiceEventData { sl = new() { 0 }, cs = 0, ri = 0 }, new List<Dungeonunitlist1>()));
    }

    [Fact]
    public void BattleAfterChoice_OneLogPerAbnoId_InRequestOrder_WithPartsFromMdAbnoUnits()
    {
        // Preserve REQUEST order (not sorted) and Distinct() - see the handler's documented
        // capture evidence (run-2 seq16 requests [8585, 8200], response keeps that order).
        var ids = new List<long> { 8585, 8200, 8585 };

        var logs = EventEngine.BattleAfterChoice(ids);

        Assert.Equal(2, logs.Count); // Distinct()
        Assert.Equal(8585, logs[0].id);
        Assert.Equal(8200, logs[1].id);
        foreach (var log in logs)
        {
            var expectedParts = MdAbnoUnits.PartsFor(log.id);
            Assert.Equal(expectedParts, log.ps.Select(p => p.id).ToList());
        }
    }
}
