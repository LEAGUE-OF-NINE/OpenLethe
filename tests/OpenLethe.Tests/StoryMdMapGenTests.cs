using System.Collections.Generic;
using System.Linq;
using OpenLethe.Server;
using OpenLethe.Server.Wire;
using Xunit;

namespace OpenLethe.Tests;

public class StoryMdMapGenTests
{
    [Fact]
    public void GenerateNewFloor_UnknownDungeonId_ThrowsPromptly()
    {
        // Without the battlePool-empty guard in GenerateNewFloor, this would HANG (not fail):
        // StoryMdThemeData.GetTheme returns an all-empty theme for unknown ids, and
        // GenerateBattleNode's unbounded retry loop never terminates against empty pools.
        var save = new StoryMirrorSaveInfo { dungeonid = 123456 };
        Assert.Throws<KeyNotFoundException>(() => StoryMdMapGen.GenerateNewFloor(0, 123456, save));
    }

    [Theory]
    [InlineData(910301)] [InlineData(910302)] [InlineData(910701)] [InlineData(910901)]
    [InlineData(911401)] [InlineData(911501)] [InlineData(911801)] [InlineData(912014)]
    [InlineData(912114)] [InlineData(912401)] [InlineData(912711)]
    public void GenerateNewFloor_TerminatesForEveryStoryMdDungeon(long dungeonId)
    {
        // REGRESSION GUARD for the upstream infinite loop: with empty pools Rust's
        // generate_battle_node spins forever. The derived pools must keep this bounded.
        var save = new StoryMirrorSaveInfo { dungeonid = dungeonId };
        StoryMdMapGen.GenerateNewFloor(0, dungeonId, save);
        Assert.NotEmpty(save.map.ns);
    }

    [Fact]
    public void GenerateNewFloor_ProducesStartRestFloorAndBossNodes()
    {
        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        StoryMdMapGen.GenerateNewFloor(0, 910301, save);
        var ns = save.map.ns;

        // Start node: sector 0, id NodeId(floor,0,0), type 0.
        var start = Assert.Single(ns, n => n.nid == MdMapGen.NodeId(0, 0, 0));
        Assert.Equal(0, start.s);
        Assert.NotEmpty(start.nnids);

        // Floor width is 7, so the tail nodes sit at sectors 8, 9 and 10 (w + 1).
        Assert.Single(ns, n => n.e == 10);               // rest
        Assert.Single(ns, n => n.e == 11);               // floor
        var boss = Assert.Single(ns, n => n.e == 6);     // boss
        Assert.NotEqual(0, boss.eid);
    }

    [Fact]
    public void GenerateNewFloor_EveryEdgeTargetExists()
    {
        var save = new StoryMirrorSaveInfo { dungeonid = 910701 };
        StoryMdMapGen.GenerateNewFloor(0, 910701, save);
        var ids = save.map.ns.Select(n => n.nid).ToHashSet();
        Assert.All(save.map.ns, n => Assert.All(n.nnids, t => Assert.Contains(t, ids)));
    }

    [Fact]
    public void GenerateNewFloor_NonZeroFloor_MovesCurrentNodeAndSetsFlags()
    {
        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.eid = 42;
        StoryMdMapGen.GenerateNewFloor(2, 910301, save);

        Assert.Equal(2, save.currentinfo.cn.f);
        Assert.Equal(0, save.currentinfo.cn.s);
        Assert.Equal(MdMapGen.NodeId(2, 0, 0), save.currentinfo.cn.nid);
        Assert.Equal(0, save.currentinfo.eid);
        Assert.Equal(2, save.currentinfo.sepsCreated);
    }

    [Fact]
    public void GenerateNewFloor_FloorZero_LeavesCurrentNodeAlone()
    {
        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        save.currentinfo.cn = new Currentnode { f = 9, s = 9, nid = 999 };
        StoryMdMapGen.GenerateNewFloor(0, 910301, save);

        Assert.Equal(999, save.currentinfo.cn.nid); // Rust only reassigns when floor != 0
        Assert.Equal(0, save.currentinfo.eid);
        Assert.Equal(2, save.currentinfo.sepsCreated);
    }

    [Fact]
    public void GenerateBattleNode_ReturnsATypeDrawnFromThePopulatedPools()
    {
        var theme = StoryMdThemeData.GetTheme(910701);
        for (var i = 0; i < 100; i++)
        {
            var node = StoryMdMapGen.GenerateBattleNode(theme);
            Assert.Contains(node.node_type, new long[] { 1, 5, 3, 2 });
            Assert.NotEqual(0, node.encounter_id);
        }
    }

    [Fact]
    public void GetRandomMdEgoGifts_ReturnsRequestedCountExcludingTier5()
    {
        var gifts = MdEgoData.GetRandomMdEgoGifts(4);
        Assert.Equal(4, gifts.Count);   // sampled WITH replacement - duplicates are legal
        Assert.All(gifts, id =>
        {
            var ego = MdEgoData.GetById(id);
            Assert.NotNull(ego);
            Assert.NotEqual("TIER_5", ego!.tag.FirstOrDefault());
        });
    }
}
