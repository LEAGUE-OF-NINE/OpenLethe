using System.Collections.Generic;
using System.Linq;
using OpenLethe.Server;
using OpenLethe.Server.Wire;
using Xunit;

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
            Assert.Equal("Penetrate", save.currentInfo.startKeyword);
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
            var picked = MdMapGen.PickThemes(0);

            Assert.True(picked.Count <= 8);
            foreach (var tfp in picked)
            {
                Assert.Equal(new List<long> { 9051, 9048, 9001, 9066 }, tfp.upegs);
                Assert.True(tfp.idx == 0 || tfp.idx == 1);
            }
        }
    }
}
