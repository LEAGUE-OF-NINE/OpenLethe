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
    public void GenerateFloor_SpiderwebWithBladeRyoshu_OnlyRevivesRyoshu()
    {
        var run = new Run();
        run.Gifts.Items.Add(new EgoGift { Id = 9273 });
        run.Party.Add(new PartyUnit { PersonalityId = 10415, CurrentHp = 0, Cm = 20 });
        run.Party.Add(new PartyUnit { PersonalityId = 10306, CurrentHp = 0, Cm = 45 });
        run.Party.Add(new PartyUnit { PersonalityId = 11115, CurrentHp = 2153, Cm = 30 });

        MapGenerator.GenerateFloor(run, EmptyMd4ThemeId, selectedIdx: 0);

        Assert.Equal(new long[] { 10000, 0, 10000 }, run.Party.Select(u => u.CurrentHp));
        Assert.Equal(new long[] { 0, 45, 30 }, run.Party.Select(u => u.Cm));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(19, 3)]
    [InlineData(20, 4)]
    [InlineData(563, 31)]
    [InlineData(1940, 100)]
    [InlineData(5000, 100)]
    public void SuperShopPercent_Is3PlusOnePer20CostSpent(long usedCost, long expected)
    {
        Assert.Equal(expected, MapGenerator.SuperShopPercent(usedCost));
    }

    [Fact]
    public void GenerateFloor_HardFloorAtFullOdds_GeneratesSuperShopAndResetsSpend()
    {
        var run = new Run { UsedCost = 1940 };
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 1001 });

        MapGenerator.GenerateFloor(run, EmptyMd4ThemeId, selectedIdx: 1);

        var shops = run.Floor.Nodes.Where(n => n.F == 1 && n.E == 10).ToList();
        Assert.NotEmpty(shops);
        Assert.All(shops, n => Assert.Equal(1, n.Eid));
        Assert.Equal(0, run.UsedCost);
    }

    [Fact]
    public void GenerateFloor_NormalFloor_NeverGeneratesSuperShopAndKeepsSpend()
    {
        var run = new Run { UsedCost = 5000 };
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 0, Tfid = 1001 });

        MapGenerator.GenerateFloor(run, EmptyMd4ThemeId, selectedIdx: 0);

        var shops = run.Floor.Nodes.Where(n => n.F == 1 && n.E == 10).ToList();
        Assert.NotEmpty(shops);
        Assert.All(shops, n => Assert.Equal(0, n.Eid));
        Assert.Equal(5000, run.UsedCost);
    }

    [Fact]
    public void RollSuperShop_RegularShop_KeepsSpendForTheNextFloor()
    {
        var run = new Run { UsedCost = 0 };
        run.Floor.Nodes.Add(new MapNode { F = 3, E = 10, Eid = 1 });
        run.Floor.Nodes.Add(new MapNode { F = 2, E = 10, Eid = 1 }); // an earlier floor: untouched

        // At 3% a regular shop turns up within a few rolls; any super shop rolled before it resets the spend.
        var sawRegular = false;
        for (var i = 0; i < 500 && !sawRegular; i++)
        {
            run.UsedCost = 10;
            MapGenerator.RollSuperShop(run, floor: 3, hard: true);
            if (run.Floor.Nodes[0].Eid == 0)
            {
                sawRegular = true;
                Assert.Equal(10, run.UsedCost);
            }
            else Assert.Equal(0, run.UsedCost);
        }
        Assert.True(sawRegular);
        Assert.Equal(1, run.Floor.Nodes[1].Eid);
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
        var run = new Run { Idx = 1 }; // floor 0 picked from the hard group
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 1001 });
        run.Floor.Current.F = 0;
        var tfpsCreatedBefore = run.TfpsCreated;

        MapGenerator.RecreateThemePool(run);

        Assert.Equal(4, run.ThemePools.Count);
        Assert.All(run.ThemePools, t => Assert.Equal(1, t.Idx)); // act = 0/5 + 1
        Assert.Equal(tfpsCreatedBefore + 1, run.TfpsCreated);
    }

    [Fact]
    public void RecreateThemePool_NormalRun_OffersNormalAndHardGroupsEveryFloor()
    {
        var themes = new OpenLethe.Server.MirrorDungeon.Data.MdThemePool().pools.Values;
        for (long floor = 0; floor < 4; floor++)
        {
            var run = new Run { Idx = 0 }; // floor 0 picked from the normal (idx 0) group
            for (long f = 0; f <= floor; f++)
                run.ThemeFloors.Add(new ThemeFloor { F = f, Idx = 1, Tfid = 1001 });
            run.Floor.Current.F = floor;

            MapGenerator.RecreateThemePool(run);

            // Both groups present, so the Normal/Hard switch can flip on this very menu.
            Assert.Equal(8, run.ThemePools.Count);
            Assert.Equal(4, run.ThemePools.Count(t => t.Idx == 0));
            Assert.Equal(4, run.ThemePools.Count(t => t.Idx == 1));
            Assert.DoesNotContain(run.ThemePools, t => t.Tfid == 1001);
            Assert.All(run.ThemePools, t =>
            {
                Assert.Equal(t.Idx == 0 ? 3 : 4, t.Egs.Count);
                var theme = themes.Single(x => x.id == t.Tfid);
                Assert.Contains(theme.exceptionConditions, c => c.dungeonIdx == t.Idx
                    && (c.selectableFloors.Count == 0 || c.selectableFloors.Contains((floor + 1) % 5)));
            });
        }
    }

    [Fact]
    public void RecreateThemePool_RunSwitchedToHard_GetsOnlyHardPacks()
    {
        var run = new Run { Idx = 1 }; // floor 0 normal, floor 1 switched to a hard pack
        run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 1001 });
        run.ThemeFloors.Add(new ThemeFloor { F = 1, Idx = 1, Tfid = 1303 });
        run.Floor.Current.F = 1;

        MapGenerator.RecreateThemePool(run);

        Assert.Equal(4, run.ThemePools.Count);
        Assert.All(run.ThemePools, t => Assert.Equal(1, t.Idx));
    }

    [Fact]
    public void RecreateThemePool_NeverOffersAPackTheRunAlreadyPlayed()
    {
        // Hard act 2 (floors 5-9): 1110 is in its tier. Played on floor 5, it must not come back.
        for (var i = 0; i < 200; i++)
        {
            var run = new Run { Idx = 2 };
            for (long f = 0; f <= 5; f++)
                run.ThemeFloors.Add(new ThemeFloor { F = f, Idx = f / 5 + 1, Tfid = f == 5 ? 1110 : 1001 });
            run.Floor.Current.F = 5;

            MapGenerator.RecreateThemePool(run);

            Assert.Equal(4, run.ThemePools.Count);
            Assert.DoesNotContain(run.ThemePools, t => t.Tfid == 1110);
        }
    }

    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(19)]
    public void RecreateThemePool_ExtremeFloors_AlwaysOffersFourActThreePacks(long currentFloor)
    {
        var run = new Run { Idx = 3 };
        for (long f = 0; f <= currentFloor; f++)
            run.ThemeFloors.Add(new ThemeFloor { F = f, Idx = f / 5 + 1, Tfid = 1001 });
        run.Floor.Current.F = currentFloor;

        MapGenerator.RecreateThemePool(run);

        Assert.Equal(4, run.ThemePools.Count);
        Assert.All(run.ThemePools, t => Assert.Equal(3, t.Idx));
    }

    [Fact]
    public void RecreateThemePool_OffersTheAccountsCustomPacks()
    {
        // An uploaded hard pack for act 1, offset 1 - the floor a floor-0 re-roll selects.
        var custom = new OpenLethe.Server.ThemeStatic
        {
            id = 990001,
            exceptionConditions = new() { new() { dungeonIdx = 1, selectableFloors = new() { 1 } } },
            egoGiftPool = new() { 9101, 9102, 9103, 9104 },
        };
        var offered = false;
        for (var i = 0; i < 200 && !offered; i++)
        {
            var run = new Run { Idx = 1 };
            run.ThemeFloors.Add(new ThemeFloor { F = 0, Idx = 1, Tfid = 1001 });
            run.Floor.Current.F = 0;

            MapGenerator.RecreateThemePool(run, new[] { custom });

            var pack = run.ThemePools.FirstOrDefault(t => t.Tfid == 990001);
            if (pack is null) continue;
            offered = true;
            Assert.Equal(4, pack.Egs.Count);
        }
        Assert.True(offered, "the custom pack was never offered");
    }

    [Fact]
    public void GenerateFloor_PackInBothGroups_RecordsTheGroupThePlayerPicked()
    {
        // All 8 floor-0 normal packs are floor-0 hard packs too; the hard copy carries 4 gifts.
        foreach (var idx in new long[] { 0, 1 })
        {
            var run = new Run();
            run.ThemePools.Add(new ThemePool { Idx = 0, Tfid = EmptyMd4ThemeId, Egs = new() { 9001, 9002, 9003 } });
            run.ThemePools.Add(new ThemePool { Idx = 1, Tfid = EmptyMd4ThemeId, Egs = new() { 9004, 9005, 9006, 9007 } });

            MapGenerator.GenerateFloor(run, EmptyMd4ThemeId, selectedIdx: idx);

            Assert.Equal(idx == 0 ? 3 : 4, Assert.Single(run.ThemeFloors).Egs.Count);
        }
    }
}
