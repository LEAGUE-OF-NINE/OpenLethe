using OpenLethe.Server;
using OpenLethe.Server.Wire;

// RNG uses Random.Shared - assert structural invariants (shape/membership/count), never
// exact ids. See memory: mirror-dungeon-rng-is-nondeterministic.
public class MdMapGenTests
{
    // Theme 1001 (mirrordungeon-theme-floor-t1.json): mapGenSequence[0].type ==
    // "CREATE_EMPTY_MD4_FLOOR", numberList [4,3,1] -> floorWidth 4. exceptionConditions
    // has dungeonIdx 0 and 1 both with selectableFloors [0], so it's also a PickThemes(0)
    // candidate in both the easy and hard pools.
    private const long EmptyMd4ThemeId = 1001;

    [Fact]
    public void NodeId_MatchesFormula()
    {
        Assert.Equal(20301, MdMapGen.NodeId(2, 3, 1));
    }

    [Fact]
    public void RandomBoolVec_LengthAndTrueCountBounds_HoldAcrossRuns()
    {
        for (var run = 0; run < 20; run++)
        {
            var bools = MdMapGen.RandomBoolVec(6, 3, 0, 2);
            Assert.Equal(6, bools.Length);
            var trueCount = bools.Count(b => b);
            Assert.InRange(trueCount, 2, 6);
        }
    }

    [Theory]
    [InlineData(1024L)] // Charm, Wander, Doubt
    [InlineData(1118L)] // Line 5
    public void GenerateNewFloor_FixedFloorWithEmptyBoss_FillsItFromFillEmptyBoss(long themeId)
    {
        var fill = MdThemeData.GetThemeById(themeId)!.mapGenSequence.Single(s => s.type_ == "FILL_EMPTY_BOSS").numberList;
        var save = new MirrorOriginSaveInfo();

        MdMapGen.GenerateNewFloor(0, themeId, save);

        var boss = Assert.Single(save.dungeonMap.ns, n => n.e == 6);
        Assert.Contains(boss.eid, fill);
    }

    [Fact]
    public void FloorOdds_ComeFromTheDungeonTable_AndFallBackToFloorFive()
    {
        var first = MdFloorOdds.ForFloor(8, 0);
        Assert.Equal(0.25, first.eventNodePercentage);
        Assert.Equal(0, first.abnormalityBattlePercentage);

        // Unknown dungeon id -> newest table; floor past the table -> its count 4 row.
        var deep = MdFloorOdds.ForFloor(-1, 999);
        Assert.Equal(4, deep.count);
        Assert.Equal(0.2, deep.eventNodePercentage);
    }

    [Fact]
    public void GenerateBattleNode_ZeroOdds_OnlyRollsRegularBattles()
    {
        var theme = MdThemeData.GetThemeById(EmptyMd4ThemeId)!;
        var odds = new MdFloorNodeOdds();
        for (var i = 0; i < 50; i++)
            Assert.Equal(1, MdMapGen.GenerateBattleNode(theme, odds).node_type);
    }

    [Fact]
    public void GenerateNewFloor_EmptyMd4Theme_ProducesStructurallyValidGraph()
    {
        for (var run = 0; run < 10; run++)
        {
            var save = new MirrorOriginSaveInfo();

            MdMapGen.GenerateNewFloor(0, EmptyMd4ThemeId, save);

            var ns = save.dungeonMap.ns;
            Assert.Single(ns, n => n.s == 0);
            Assert.True(ns.Count(n => n.e == 6) >= 1, "expected at least one boss node (e==6)");
            Assert.True(ns.Count(n => n.e == 10) >= 1, "expected at least one shop node (e==10)");

            Assert.Single(save.currentInfo.tfs, t => t.tfid == EmptyMd4ThemeId);
            // GenerateNewFloor no longer sets startKeyword (it's the run affinity, set upstream by
            // AcquireStart and preserved across selects) - a fresh save's default "" is untouched.
            Assert.Equal("", save.currentInfo.startKeyword);
            Assert.Equal(2, save.currentInfo.sepsCreated);
            Assert.Equal(0, save.isEndDungeon);

            // graph integrity: every nnids target must reference a real node in the floor.
            var nidSet = new HashSet<long>(ns.Select(n => n.nid));
            foreach (var n in ns)
            {
                foreach (var target in n.nnids)
                {
                    Assert.Contains(target, nidSet);
                }
            }

            // boss node must be reachable via a BFS from the start node.
            var byId = ns.ToDictionary(n => n.nid);
            var start = ns.Single(n => n.s == 0);
            var bossIds = new HashSet<long>(ns.Where(n => n.e == 6).Select(n => n.nid));
            var visited = new HashSet<long> { start.nid };
            var queue = new Queue<long>();
            queue.Enqueue(start.nid);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var next in byId[cur].nnids)
                {
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }

            Assert.True(bossIds.Overlaps(visited), "boss node must be reachable from the start node");
        }
    }

    [Fact]
    public void GenerateNewFloor_UnknownTheme_Throws()
    {
        var save = new MirrorOriginSaveInfo();
        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
            () => MdMapGen.GenerateNewFloor(0, 999999999, save));
    }

    [Fact]
    public void PickThemes_Floor0_InvariantsHoldAcrossRuns()
    {
        for (var run = 0; run < 10; run++)
        {
            var picked = MdMapGen.PickThemes(0, null, 4);

            Assert.True(picked.Count <= 8);
            foreach (var tfp in picked)
            {
                Assert.Equal(new List<long> { 9051, 9048, 9001, 9066 }, tfp.upegs);
                Assert.True(tfp.idx == 0 || tfp.idx == 1);
            }
        }
    }

    // Theme 1501 (theme-floor-t6): an act-3 fixed floor - start, sector-1 events at idx 0 and 2,
    // ... shop, boss. md-extreme-run-2 played it at floor index 10.
    private const long FixedAct3ThemeId = 1501;

    [Fact]
    public void HiddenBattleInfo_IsReadFromTheCurrentSeasonsCommonData()
    {
        // mirror-dungeon-common-data-md8.json hiddenBattleInfo.
        var info = MdCommonTable.HiddenBattle;
        Assert.Equal(11, info.minFloorCondition);
        Assert.Equal(new long[] { 9000005, 9000006, 9000007, 9000008 }, info.pool);
        Assert.Equal(0.1, info.ProbForFloor(10)); // floor index 10 = floor 11
        Assert.Equal(0, info.ProbForFloor(9));
        Assert.True(MdMapGen.IsHiddenBattle(9000007));
        Assert.Equal(new long[] { 9256 }, MdMapGen.HiddenGiftsOf(9000007));
    }

    [Fact]
    public void GenerateNewFloor_NeverPlacesAHiddenNode()
    {
        // Hidden bosses are revealed when an event is entered (MapNodeRules), never generated:
        // at the shipped 10% a pre-placing generator would hit within a few hundred floors.
        for (var i = 0; i < 300; i++)
        {
            var save = new MirrorOriginSaveInfo();
            MdMapGen.GenerateNewFloor(10, FixedAct3ThemeId, save);
            Assert.DoesNotContain(save.dungeonMap.ns, n => n.e == MdMapGen.HiddenBattleNodeType);
        }
    }

    [Fact]
    public void UnbeatenHiddenBattles_SkipsStagesWhoseGiftTheRunOwns()
    {
        var info = new MdHiddenBattleInfo { pool = new() { 9000007, 9000006 } };

        Assert.Equal(new long[] { 9000006 }, MdMapGen.UnbeatenHiddenBattles(info, new long[] { 9256 })); // 9000007's gift
        Assert.Equal(new long[] { 9000007, 9000006 }, MdMapGen.UnbeatenHiddenBattles(info, Array.Empty<long>()));
    }

    [Fact]
    public void GenerateNewFloor_NeverRepeatsAnEventOnOneFloor()
    {
        foreach (var theme in MdThemeData.AllThemes())
        {
            for (var run = 0; run < 5; run++)
            {
                var save = new MirrorOriginSaveInfo();

                MdMapGen.GenerateNewFloor(3, theme.id, save);

                var eids = save.dungeonMap.ns.Where(n => n.e == 3).Select(n => n.eid).ToList();
                Assert.True(eids.Count == eids.Distinct().Count(),
                    $"theme {theme.id} repeated an event: {string.Join(",", eids)}");
            }
        }
    }
}
