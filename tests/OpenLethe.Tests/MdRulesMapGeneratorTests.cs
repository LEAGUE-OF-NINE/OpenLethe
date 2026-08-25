using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.MirrorDungeon.Rules;

namespace OpenLethe.Tests;

// Component tests for the Task 5 domain primitive: MapGenerator (floor generation + theme-pool
// re-roll), ported from Handlers/MirrorDungeonMap.cs SelectThemeFloor (:227-296) and
// RecreateThemeFloorPool (:198-225). RNG content (node ids/eids/pool ids) is non-deterministic
// (Random.Shared - see memory: mirror-dungeon-rng-is-nondeterministic) so these assert STRUCTURE
// (node count/keying, party full-heal, tfpsCreated, startKeyword preserved), never golden values.
// The existing MdMapGenTests.cs (wire-typed MdMapGen) is untouched - MapGenerator WRAPS it.
public class MdRulesMapGeneratorTests
{
    private const long EmptyMd4ThemeId = 1001; // mirrordungeon-theme-floor-t1.json

    [Fact]
    public void GenerateFloor_Floor0_GeneratesFloorKeyedMapAndRecordsThemeFloor()
    {
        var run = new Run { StartKeyword = "Combustion" };

        MapGenerator.GenerateFloor(run, EmptyMd4ThemeId, selectedIdx: 0);

        Assert.NotEmpty(run.Floor.Nodes);
        Assert.All(run.Floor.Nodes, n => Assert.Equal(0, n.F));
        Assert.Contains(run.Floor.Nodes, n => n.E == 6); // boss
        Assert.Contains(run.Floor.Nodes, n => n.E == 10); // shop
        var tfloor = Assert.Single(run.ThemeFloors);
        Assert.Equal(EmptyMd4ThemeId, tfloor.Tfid);
        Assert.Equal(0, tfloor.F);
        // startKeyword is the run affinity set upstream by AcquireStart - preserved, not clobbered.
        Assert.Equal("Combustion", run.StartKeyword);
        // Floor 0 leaves the pre-existing eid untouched (only floors >= 1 reset it to 0).
        Assert.Equal(0, run.Eid);
    }

    [Fact]
    public void GenerateFloor_NonZeroFloor_FloorIndexFromThemeFloorsCount_NotLevelAdders()
    {
        var run = new Run { Eid = 999 };
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 1001 });
        run.ThemeFloors.Add(new ThemeFloor { F = 1, Idx = 1, Tfid = 1001 });
        run.LevelAdders.Add(1); // deliberately mismatched vs ThemeFloors.Count (2)

        MapGenerator.GenerateFloor(run, EmptyMd4ThemeId, selectedIdx: 2);

        Assert.Equal(2, run.Floor.Current.F);
        Assert.Equal(20000, run.Floor.Current.Nid);
        Assert.Equal(0, run.Eid); // reset on floors >= 1
        var newFloor = run.ThemeFloors[^1];
        Assert.Equal(2, newFloor.F);
        Assert.Equal(1, newFloor.Idx); // act = floor/5 + 1
        var floorNodes = run.Floor.Nodes.Where(n => n.Nid >= 20000 && n.Nid < 30000).ToList();
        Assert.NotEmpty(floorNodes);
        Assert.All(floorNodes, n => Assert.Equal(2, n.F));
        // ChoiceEventList == this floor's e==3 event-node eids.
        Assert.Equal(
            floorNodes.Where(n => n.E == 3).Select(n => n.Eid).OrderBy(x => x),
            run.ChoiceEventList.OrderBy(x => x));
    }

    [Fact]
    public void GenerateFloor_FullHealsParty_ClearsCmOnlyForRevivedUnits()
    {
        var run = new Run();
        run.Party.Add(new PartyUnit { PersonalityId = 1, CurrentHp = 3000, Cm = 12 }); // wounded, alive
        run.Party.Add(new PartyUnit { PersonalityId = 2, CurrentHp = 0, Cm = 40 });    // revived from dead

        MapGenerator.GenerateFloor(run, EmptyMd4ThemeId, selectedIdx: 0);

        var u1 = run.Party.Single(u => u.PersonalityId == 1);
        var u2 = run.Party.Single(u => u.PersonalityId == 2);
        Assert.Equal(10000, u1.CurrentHp);
        Assert.Equal(12, u1.Cm); // alive unit's cm preserved
        Assert.Equal(10000, u2.CurrentHp);
        Assert.Equal(0, u2.Cm); // revived unit's cm cleared
    }

    [Fact]
    public void GenerateFloor_ResetsTfpsCreatedAndSetsIdx()
    {
        var run = new Run { TfpsCreated = 3 };

        MapGenerator.GenerateFloor(run, EmptyMd4ThemeId, selectedIdx: 2);

        Assert.Equal(0, run.TfpsCreated);
        Assert.Equal(2, run.Idx);
    }

    [Fact]
    public void GenerateFloor_UnknownTheme_Throws()
    {
        var run = new Run();
        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
            () => MapGenerator.GenerateFloor(run, 999999999, selectedIdx: 0));
    }

    [Fact]
    public void RecreateThemePool_NoThemeFloorsYet_UsesFloor0PickPool()
    {
        var run = new Run();

        MapGenerator.RecreateThemePool(run);

        Assert.True(run.ThemePools.Count > 0);
        Assert.Equal(1, run.TfpsCreated);
        Assert.Empty(run.StartPools);
    }

    [Fact]
    public void RecreateThemePool_AfterFloorsSelected_RerollsFourThemesForNextFloor()
    {
        var run = new Run();
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 1001 });
        run.Floor.Current.F = 0;
        var tfpsCreatedBefore = run.TfpsCreated;

        MapGenerator.RecreateThemePool(run);

        Assert.Equal(4, run.ThemePools.Count);
        Assert.All(run.ThemePools, t => Assert.Equal(1, t.Idx)); // act = 0/5 + 1
        Assert.Equal(tfpsCreatedBefore + 1, run.TfpsCreated);
    }
}
