using System.Linq;
using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.MirrorDungeon.Rules;
using Xunit;

namespace OpenLethe.Tests;

// Component tests for the Task 2 domain primitives: SharedRules (Run-mutating helpers) +
// Effects (pure constant lookups). Mirrors the wire-typed assertions in
// MirrorDungeonMapHandlerTests.GrantEgoGift_OwnedTier2Or3_UpgradesToSuperWithOid_ElseAddsAsIs,
// translated onto the domain Run/EgoGift types.
public class MdRulesSharedTests
{
    [Fact]
    public void GrantEgoGift_NewGift_AppendsAsIs()
    {
        var run = new Run();
        SharedRules.GrantEgoGift(run, 9004);
        Assert.Equal(9004, run.Gifts.Items[^1].Id);
        Assert.Null(run.Gifts.Items[^1].Oid);
    }

    [Fact]
    public void GrantEgoGift_OwnedTier3_UpgradesToSuperVestigeWithOid()
    {
        var run = new Run();
        run.Gifts.Items.Add(new EgoGift { Id = 9053 }); // 9053 is TIER_3.
        SharedRules.GrantEgoGift(run, 9053);
        Assert.Equal(9993, run.Gifts.Items[^1].Id);
        Assert.Equal(9053, run.Gifts.Items[^1].Oid);
        Assert.Contains(run.Gifts.Items, g => g.Id == 9053 && g.Oid is null);
    }

    [Fact]
    public void GrantEgoGift_OwnedTier2_UpgradesToSuperVestigeWithOid()
    {
        var run = new Run();
        run.Gifts.Items.Add(new EgoGift { Id = 9155 }); // 9155 is TIER_2.
        SharedRules.GrantEgoGift(run, 9155);
        Assert.Equal(9992, run.Gifts.Items[^1].Id);
        Assert.Equal(9155, run.Gifts.Items[^1].Oid);
    }

    [Fact]
    public void GrantEgoGift_OwnedUntieredEnemyBuffGift_AddsAsIs()
    {
        var run = new Run();
        run.Gifts.Items.Add(new EgoGift { Id = 992206 });
        SharedRules.GrantEgoGift(run, 992206);
        Assert.Equal(992206, run.Gifts.Items[^1].Id);
        Assert.Null(run.Gifts.Items[^1].Oid);
    }

    [Fact]
    public void RecomputeEgmlos_SumsHiddenGiftBumps()
    {
        var run = new Run();
        run.Gifts.Items.Add(new EgoGift { Id = 993003 }); // +2
        run.Gifts.Items.Add(new EgoGift { Id = 993005 }); // +3
        run.Gifts.Items.Add(new EgoGift { Id = 9001 });   // +0, normal gift
        SharedRules.RecomputeEgmlos(run);
        Assert.Equal(5, run.LevelOffsets.Egmlos);
    }

    [Fact]
    public void RecomputeEgmlos_NoHiddenGifts_IsZero()
    {
        var run = new Run();
        run.Gifts.Items.Add(new EgoGift { Id = 9001 });
        SharedRules.RecomputeEgmlos(run);
        Assert.Equal(0, run.LevelOffsets.Egmlos);
    }

    [Fact]
    public void Effects_HiddenGiftLevelBump_MatchesKnownValues()
    {
        Assert.Equal(2, Effects.HiddenGiftLevelBump(993003));
        Assert.Equal(3, Effects.HiddenGiftLevelBump(993005));
        Assert.Equal(0, Effects.HiddenGiftLevelBump(9001));
    }

    [Fact]
    public void CurrentFloor_IsLevelAddersCount()
    {
        var run = new Run();
        run.LevelAdders.Add(1);
        run.LevelAdders.Add(2);
        Assert.Equal(2, SharedRules.CurrentFloor(run));
    }

    [Fact]
    public void ThemePackId_DefaultsTo1001WhenNoThemeFloors()
    {
        var run = new Run();
        Assert.Equal(1001, SharedRules.ThemePackId(run));
    }

    [Fact]
    public void ThemePackId_UsesLastThemeFloorTfid()
    {
        var run = new Run();
        run.ThemeFloors.Add(new ThemeFloor { Tfid = 1108 });
        run.ThemeFloors.Add(new ThemeFloor { Tfid = 1203 });
        Assert.Equal(1203, SharedRules.ThemePackId(run));
    }

    [Fact]
    public void IsSuperShop_NullWhenNodeMissingOrNotShop()
    {
        var run = new Run();
        run.Floor.Current.Nid = 1;
        run.Floor.Nodes.Add(new MapNode { Nid = 1, E = 1 }); // not a shop node
        Assert.Null(SharedRules.IsSuperShop(run));

        var run2 = new Run();
        run2.Floor.Current.Nid = 999; // no matching node
        Assert.Null(SharedRules.IsSuperShop(run2));
    }

    [Fact]
    public void IsSuperShop_TrueWhenShopNodeHasNonZeroEid()
    {
        var run = new Run();
        run.Floor.Current.Nid = 1;
        run.Floor.Nodes.Add(new MapNode { Nid = 1, E = 10, Eid = 5 });
        Assert.True(SharedRules.IsSuperShop(run));
    }

    [Fact]
    public void IsSuperShop_FalseWhenShopNodeHasZeroEid()
    {
        var run = new Run();
        run.Floor.Current.Nid = 1;
        run.Floor.Nodes.Add(new MapNode { Nid = 1, E = 10, Eid = 0 });
        Assert.False(SharedRules.IsSuperShop(run));
    }

    [Fact]
    public void ShopGiftCount_MatchesSuperShopTiers()
    {
        var normal = new Run();
        normal.Floor.Current.Nid = 1;
        normal.Floor.Nodes.Add(new MapNode { Nid = 1, E = 10, Eid = 0 });
        Assert.Equal(5, SharedRules.ShopGiftCount(normal));

        var super = new Run();
        super.Floor.Current.Nid = 1;
        super.Floor.Nodes.Add(new MapNode { Nid = 1, E = 10, Eid = 1 });
        Assert.Equal(10, SharedRules.ShopGiftCount(super));

        var notShop = new Run();
        notShop.Floor.Current.Nid = 1;
        notShop.Floor.Nodes.Add(new MapNode { Nid = 1, E = 1 });
        Assert.Equal(0, SharedRules.ShopGiftCount(notShop));
    }

    [Fact]
    public void MergeParty_PreservesUpidxByPidAndClearsPordAndEgoGauge()
    {
        var prior = new System.Collections.Generic.List<PartyUnit>
        {
            new() { PersonalityId = 1, UpgradeIndices = new() { 3, 4 } },
        };
        var incoming = new System.Collections.Generic.List<PartyUnit>
        {
            new()
            {
                PersonalityId = 1,
                UpgradeIndices = new(), // client clears this - must be restored from prior.
                Pord = 0,
                EgoSkills = new() { new EgoSkill { Id = 5, G = 42 } },
            },
        };

        var merged = SharedRules.MergeParty(prior, incoming);

        var unit = Assert.Single(merged);
        Assert.Equal(new long[] { 3, 4 }, unit.UpgradeIndices);
        Assert.Equal(-1, unit.Pord);
        Assert.Equal(0, unit.EgoSkills[0].G);
    }

    [Fact]
    public void ApplyShopDiscount_AppliesSeventyPercentWhenGiftOwned()
    {
        var run = new Run();
        run.Gifts.Items.Add(new EgoGift { Id = 9191 });
        Assert.Equal(70, Effects.ApplyShopDiscount(run, 9191, 100));
    }

    [Fact]
    public void ApplyShopDiscount_FullPriceWhenGiftNotOwned()
    {
        var run = new Run();
        Assert.Equal(100, Effects.ApplyShopDiscount(run, 9191, 100));
    }

    [Fact]
    public void StartingCost_Dungeon7_Is200_OtherwiseDefault500()
    {
        Assert.Equal(200, Effects.StartingCost(7));
        Assert.Equal(500, Effects.StartingCost(4));
    }
}
