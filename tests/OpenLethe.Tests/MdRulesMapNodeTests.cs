using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.MirrorDungeon.Rules;
using OpenLethe.Server.Wire;

namespace OpenLethe.Tests;

// Component tests for the Task 6 domain primitive: MapNodeRules.EnterNode, ported from
// Handlers/MirrorDungeonMap.cs EnterMirrorDungeonMapNode (:244-306). Structure/behavior only -
// the shop slot COMPOSITION is RNG (masked in the replay); only its count is byte-guaranteed.
// LevelOffsetEngine is deliberately NOT extracted here: EnterMapNode never touches
// run.LevelOffsets (sbmlos/egmlos/snft/csnft belong to SelectThemeFloor/ExitMapNode) - see the
// Task 6 brief's YAGNI clause.
public class MdRulesMapNodeTests
{
    private static Run RunWithNode(long nid, long e, long eid = 0)
    {
        var run = new Run();
        run.Floor.Nodes.Add(new MapNode { Nid = nid, E = e, Eid = eid });
        return run;
    }

    [Theory]
    [InlineData(3)]  // event node
    [InlineData(10)] // shop node
    public void EnterNode_EventOrShopNode_NrIsThree(long e)
    {
        var run = RunWithNode(nid: 100, e: e);

        var (changedHiddenNode, nr) = MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 100 });

        Assert.Equal(3, nr);
        Assert.Equal(3, run.Nr);
        Assert.False(changedHiddenNode);
    }

    [Theory]
    [InlineData(1)]  // normal battle
    [InlineData(2)]  // abno battle
    [InlineData(5)]  // hard battle
    [InlineData(6)]  // boss
    [InlineData(14)] // hard abno
    public void EnterNode_BattleTypeNode_NrIsFour(long e)
    {
        var run = RunWithNode(nid: 100, e: e);

        var (_, nr) = MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 100 });

        Assert.Equal(4, nr);
        Assert.Equal(4, run.Nr);
    }

    [Fact]
    public void EnterNode_UpdatesFloorCurrentPositionToEnteredNode()
    {
        var run = RunWithNode(nid: 200, e: 1);

        MapNodeRules.EnterNode(run, new Currentnode { f = 2, s = 1, nid = 200 });

        Assert.Equal(2, run.Floor.Current.F);
        Assert.Equal(1, run.Floor.Current.S);
        Assert.Equal(200, run.Floor.Current.Nid);
    }

    [Fact]
    public void EnterNode_SetsEidSideEffectFromEnteredNode()
    {
        var run = RunWithNode(nid: 300, e: 1, eid: 4242);

        MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 300 });

        Assert.Equal(4242, run.Eid);
    }

    [Fact]
    public void EnterNode_BelowHiddenBattleFloors_NeverRevealsOne()
    {
        var run = RunWithNode(nid: 100, e: 3, eid: 901091);

        var (changedHiddenNode, _) = MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 100 });

        Assert.False(changedHiddenNode);
        Assert.Equal(3, run.Floor.Nodes[0].E);
    }

    // hiddenBattleInfo that always hits on floor 11 (index 10).
    private static OpenLethe.Server.MdHiddenBattleInfo AlwaysHidden(params long[] pool) => new()
    {
        minFloorCondition = 11,
        pool = pool.ToList(),
        probInfo = new() { new OpenLethe.Server.MdHiddenBattleProb { floor = 11, prob = 1.0 } },
    };

    private static Run Floor10Run()
    {
        var run = new Run();
        run.Floor.Current = new CurrentPosition { F = 10, S = 0, Nid = 100000 };
        run.Floor.Nodes.Add(new MapNode { F = 10, S = 1, Nid = 100102, E = 3, Eid = 901091, Nnids = new() { 100202 } });
        run.Floor.Nodes.Add(new MapNode { F = 10, S = 1, Nid = 100101, E = 1, Eid = 2066431 });
        return run;
    }

    [Fact]
    public void EnterNode_EventOnHiddenFloor_RevealsHiddenBossLikeRetail()
    {
        // md-extreme-run-2 seq334 -> 335: 100102 goes e 3 eid 901091 -> e 15 eid 9000007,
        // keeps its edges, and the entry answers changedHiddenNode true with nr 5.
        var run = Floor10Run();

        var (changedHiddenNode, nr) = MapNodeRules.EnterNode(run,
            new Currentnode { f = 10, s = 1, nid = 100102 }, AlwaysHidden(9000007));

        Assert.True(changedHiddenNode);
        Assert.Equal(5, nr);
        var node = run.Floor.Nodes[0];
        Assert.Equal(15, node.E);
        Assert.Equal(9000007, node.Eid);
        Assert.Equal(new long[] { 100202 }, node.Nnids);
        Assert.Equal(9000007, run.Eid);
        var origin = Assert.Single(run.HiddenNodeOrigins);
        Assert.Equal((100102L, 901091L), (origin.Nid, origin.Eid));
    }

    [Fact]
    public void EnterNode_BattleNodeOnHiddenFloor_IsNeverRevealed()
    {
        var run = Floor10Run();

        var (changedHiddenNode, nr) = MapNodeRules.EnterNode(run,
            new Currentnode { f = 10, s = 1, nid = 100101 }, AlwaysHidden(9000007));

        Assert.False(changedHiddenNode);
        Assert.Equal(4, nr);
        Assert.Equal(1, run.Floor.Nodes[1].E);
    }

    [Fact]
    public void EnterNode_ReenteringRevealedNode_StartsTheFightWithoutRerolling()
    {
        // seq336/338: the client enters the revealed node again with its party - nr 4, no flag.
        var run = Floor10Run();
        var at = new Currentnode { f = 10, s = 1, nid = 100102 };
        MapNodeRules.EnterNode(run, at, AlwaysHidden(9000007));

        var (changedHiddenNode, nr) = MapNodeRules.EnterNode(run, at, AlwaysHidden(9000006));

        Assert.False(changedHiddenNode);
        Assert.Equal(4, nr);
        Assert.Equal(9000007, run.Floor.Nodes[0].Eid);
    }

    [Fact]
    public void EnterNode_AllHiddenBossesBeaten_RevealsNothing()
    {
        var run = Floor10Run();
        run.Gifts.Items.Add(new EgoGift { Id = 9256 }); // 9000007's exclusive gift

        var (changedHiddenNode, _) = MapNodeRules.EnterNode(run,
            new Currentnode { f = 10, s = 1, nid = 100102 }, AlwaysHidden(9000007));

        Assert.False(changedHiddenNode);
        Assert.Equal(3, run.Floor.Nodes[0].E);
    }

    [Fact]
    public void EscapeHiddenNode_RestoresTheEventAndClearsTheNodeWithoutRewards()
    {
        var run = Floor10Run();
        var at = new Currentnode { f = 10, s = 1, nid = 100102 };
        MapNodeRules.EnterNode(run, at, AlwaysHidden(9000007));
        run.RewardEvents.Add(new RewardEvent { Rt = "GetBattleRewardCase" }); // must not survive

        Assert.True(MapNodeRules.EscapeHiddenNode(run, at));

        var node = run.Floor.Nodes[0];
        Assert.Equal((3L, 901091L), (node.E, node.Eid));
        Assert.Equal(901091, run.Eid);
        Assert.Equal(100102, run.Floor.Current.Nid);
        Assert.Empty(run.HiddenNodeOrigins);
        // Cleared like an exit, so the client moves on: in pnids, nr 1, nothing to collect.
        Assert.Equal(new long[] { 100102 }, run.Pnids);
        Assert.Equal(1, run.Nr);
        Assert.Empty(run.RewardEvents);
        Assert.Empty(run.Peids);
        Assert.DoesNotContain(run.Gifts.Items, g => g.Id == 9256);

        // A second skip (client retry) does not record the node twice.
        node.E = 15;
        MapNodeRules.EscapeHiddenNode(run, at);
        Assert.Equal(new long[] { 100102 }, run.Pnids);
    }

    [Fact]
    public void EscapeHiddenNode_NodeWithoutOrigin_BecomesAnEvent()
    {
        // A save from before reveals happened on entry has the hidden node pre-placed.
        var run = new Run();
        run.Floor.Nodes.Add(new MapNode { F = 12, S = 1, Nid = 120102, E = 15, Eid = 9000006 });

        Assert.True(MapNodeRules.EscapeHiddenNode(run, new Currentnode { f = 12, s = 1, nid = 120102 }));

        Assert.Equal(3, run.Floor.Nodes[0].E);
        Assert.NotEqual(0, run.Floor.Nodes[0].Eid);
    }

    [Fact]
    public void EscapeHiddenNode_NotAHiddenNode_ChangesNothing()
    {
        var run = Floor10Run();

        Assert.False(MapNodeRules.EscapeHiddenNode(run, new Currentnode { f = 10, s = 1, nid = 100102 }));
        Assert.Equal(3, run.Floor.Nodes[0].E);
    }

    [Fact]
    public void EnterNode_NonShopNode_ShopUntouched()
    {
        var run = RunWithNode(nid: 100, e: 1);
        run.Shop.Rc = 7; // pre-existing shop state must survive a non-shop entry untouched

        MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 100 });

        Assert.Equal(7, run.Shop.Rc);
        Assert.Empty(run.Shop.Slots);
    }

    [Fact]
    public void EnterNode_NormalShopNode_UsesRetailSlotTable()
    {
        // eid==0 -> normal shop: shopInfoByType[0] = 1 named "up" slot + 4 gift slots.
        var run = RunWithNode(nid: 100, e: 10, eid: 0);
        run.Party.Add(new PartyUnit { PersonalityId = 11, CurrentHp = 10000 });
        run.Party.Add(new PartyUnit { PersonalityId = 12, CurrentHp = 10000 });

        MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 100 });

        Assert.Equal(new[] { "up", "eg", "eg", "eg", "eg" }, run.Shop.Slots.Select(s => s.T));
        Assert.Contains(run.Shop.Slots[0].Id, new long[] { 11, 12 });
        Assert.All(run.Shop.Slots, s => Assert.Equal(1, s.S));
    }

    [Fact]
    public void EnterNode_ShopNode_NeverOffersOwnedGifts()
    {
        // Own the whole shop pool except three gifts: those three are all that can show up.
        var pool = new OpenLethe.Server.MirrorDungeon.Data.MdThemePool().ShopEgoGiftsPool(1001);
        var run = RunWithNode(nid: 100, e: 10, eid: 0);
        run.Gifts.Items.AddRange(pool.Skip(3).Select(id => new EgoGift { Id = id }));

        MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 100 });

        var offered = run.Shop.Slots.Where(s => s.T == "eg").Select(s => s.Id).ToList();
        Assert.NotEmpty(offered);
        Assert.All(offered, id => Assert.Contains(id, pool.Take(3)));
    }

    [Fact]
    public void EnterNode_ShopOnCustomPackFloor_StocksThePacksGifts()
    {
        // 9701 is pack 1101's exclusive; an uploaded pack listing it must get it on sale. Own every
        // other shop gift so the custom pack's two are the only ones left to stock.
        var custom = new OpenLethe.Server.ThemeStatic { id = 990001, egoGiftPool = new() { 9701, 9101 } };
        var shopPool = new OpenLethe.Server.MirrorDungeon.Data.MdThemePool(new[] { custom }).ShopEgoGiftsPool(990001);
        var run = RunWithNode(nid: 100, e: 10, eid: 0);
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 990001 });
        run.LevelAdders.AddRange(new long[] { 1, 1 }); // floor 3+: every gift tier has shop weight
        run.Gifts.Items.AddRange(shopPool.Where(id => id is not (9701 or 9101)).Select(id => new EgoGift { Id = id }));

        MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 100 }, customThemes: new[] { custom });

        Assert.Equal(new long[] { 9101, 9701 }, run.Shop.Slots.Where(s => s.T == "eg").Select(s => s.Id).OrderBy(x => x));
    }

    [Fact]
    public void EnterNode_SuperShopNode_UsesRetailSlotTable()
    {
        // eid!=0 -> super shop: shopInfoByType[1] = 1 "upt" + 2 named "up" + 7 gift slots,
        // targeting slot first (md-extreme-run seq67/96).
        var run = RunWithNode(nid: 100, e: 10, eid: 1);
        run.Party.Add(new PartyUnit { PersonalityId = 11, CurrentHp = 10000 });
        run.Party.Add(new PartyUnit { PersonalityId = 12, CurrentHp = 10000 });
        run.Party.Add(new PartyUnit { PersonalityId = 13, CurrentHp = 10000 });

        MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 100 });

        Assert.Equal(new[] { "upt", "up", "up" }, run.Shop.Slots.Take(3).Select(s => s.T));
        var named = run.Shop.Slots.Skip(1).Take(2).Select(s => s.Id).ToList();
        Assert.Equal(2, named.Distinct().Count());
        Assert.All(named, id => Assert.Contains(id, new long[] { 11, 12, 13 }));
        Assert.Equal(7, run.Shop.Slots.Count(s => s.T == "eg"));
    }

    [Fact]
    public void EnterNode_ShopNode_OffersSkillReplacementToRandomLivingSinners()
    {
        // The named slot used to always go to party[0]; it must be drawn from every living sinner
        // and never from a dead one.
        var seen = new HashSet<long>();
        for (var i = 0; i < 200; i++)
        {
            var run = RunWithNode(nid: 100, e: 10, eid: 0);
            run.Party.Add(new PartyUnit { PersonalityId = 11, CurrentHp = 10000 });
            run.Party.Add(new PartyUnit { PersonalityId = 12, CurrentHp = 0 });
            run.Party.Add(new PartyUnit { PersonalityId = 13, CurrentHp = 500 });
            run.Party.Add(new PartyUnit { PersonalityId = 14, CurrentHp = 10000 });

            MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 100 });

            seen.Add(run.Shop.Slots.Single(s => s.T == "up").Id);
        }

        Assert.Equal(new long[] { 11, 13, 14 }, seen.OrderBy(x => x));
    }

    [Fact]
    public void EnterNode_ShopNode_FreshShopResetsPriorScalars()
    {
        var run = RunWithNode(nid: 100, e: 10, eid: 0);
        run.Shop.Rc = 3;
        run.Shop.Fre = 2;

        MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 100 });

        Assert.Equal(0, run.Shop.Rc);
        Assert.Equal(0, run.Shop.Fre);
    }
}
