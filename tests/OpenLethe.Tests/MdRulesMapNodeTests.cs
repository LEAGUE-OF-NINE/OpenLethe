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
    public void EnterNode_AlwaysReturnsChangedHiddenNodeFalse()
    {
        var run = RunWithNode(nid: 100, e: 1);

        var (changedHiddenNode, _) = MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 100 });

        Assert.False(changedHiddenNode);
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
    public void EnterNode_NormalShopNode_GeneratesFiveSlots()
    {
        var run = RunWithNode(nid: 100, e: 10, eid: 0); // eid==0 -> normal shop (ShopGiftCount==5)

        MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 100 });

        Assert.Equal(5, run.Shop.Slots.Count);
        Assert.All(run.Shop.Slots, s => Assert.Equal("eg", s.T));
        Assert.All(run.Shop.Slots, s => Assert.Equal(1, s.S));
    }

    [Fact]
    public void EnterNode_SuperShopNode_GeneratesTenSlots()
    {
        var run = RunWithNode(nid: 100, e: 10, eid: 1); // eid!=0 -> super shop (ShopGiftCount==10)

        MapNodeRules.EnterNode(run, new Currentnode { f = 0, s = 0, nid = 100 });

        Assert.Equal(10, run.Shop.Slots.Count);
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
