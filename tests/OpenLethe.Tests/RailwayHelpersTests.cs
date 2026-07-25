using System.Collections.Generic;
using System.Linq;
using OpenLethe.Server;
using OpenLethe.Server.Wire;
using Xunit;

public class RailwayHelpersTests
{
    [Fact]
    public void UpsertNode_ReplacesSameNodeId_ElseAppends()
    {
        var nodes = new List<UpdateNodeDatas> { new() { nodeid = 1, clearturn = 5 } };
        RailwayHelpers.UpsertNode(nodes, new UpdateNodeDatas { nodeid = 1, clearturn = 9 });
        Assert.Single(nodes);
        Assert.Equal(9, nodes[0].clearturn);

        RailwayHelpers.UpsertNode(nodes, new UpdateNodeDatas { nodeid = 2 });
        Assert.Equal(2, nodes.Count);
    }

    [Fact]
    public void FindOrDefaultNode_ReturnsExisting_OrAppendsDefault()
    {
        var nodes = new List<UpdateNodeDatas> { new() { nodeid = 1 } };
        var found = RailwayHelpers.FindOrDefaultNode(nodes, 1);
        Assert.Same(nodes[0], found);

        var made = RailwayHelpers.FindOrDefaultNode(nodes, 7);
        Assert.Equal(7, made.nodeid);
        Assert.Equal(2, nodes.Count);
        Assert.Same(nodes[1], made); // returns the appended element by reference
    }

    [Fact]
    public void UpsertBuff_ByNid()
    {
        var buffs = new List<Buffsetsbyegogift> { new() { nid = 1 } };
        RailwayHelpers.UpsertBuff(buffs, new Buffsetsbyegogift { nid = 1, buffs = new() { new Buffs1 { buffId = 3 } } });
        Assert.Single(buffs);
        Assert.Equal(3, buffs[0].buffs[0].buffId);

        RailwayHelpers.UpsertBuff(buffs, new Buffsetsbyegogift { nid = 2 });
        Assert.Equal(2, buffs.Count);
    }

    [Fact]
    public void BuffsBelowNode_FiltersNidStrictlyLess()
    {
        var buffs = new List<Buffsetsbyegogift> { new() { nid = 0 }, new() { nid = 1 }, new() { nid = 2 } };
        var below = RailwayHelpers.BuffsBelowNode(buffs, 2);
        Assert.Equal(new long[] { 0, 1 }, below.Select(b => b.nid).ToArray());
    }
}
