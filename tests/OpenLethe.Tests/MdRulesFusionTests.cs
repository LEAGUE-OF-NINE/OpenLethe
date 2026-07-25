using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.MirrorDungeon.Rules;
using Xunit;

namespace OpenLethe.Tests;

// Component tests for the Task 11 fusion rules: FusionRules (Run-mutating), delegating to the
// shared MdEgoFusion reader (unchanged, not moved). Mirrors the fixed-recipe/tier-upgrade
// assertions documented in the handler-migration ledger, translated onto the domain Run/EgoGift
// types. Structure/behavior, not golden RNG for the non-fixed roll path.
public class MdRulesFusionTests
{
    [Fact]
    public void Combine_FixedRecipe_RemovesMaterialsAndAddsResult()
    {
        // Golden fixed recipe (same as MdEgoFusionTests): 9003+9053+9157 -> 9088.
        var run = new Run();
        run.Gifts.Items.Add(new EgoGift { Id = 9003 });
        run.Gifts.Items.Add(new EgoGift { Id = 9053 });
        run.Gifts.Items.Add(new EgoGift { Id = 9157 });
        run.Gifts.Items.Add(new EgoGift { Id = 12345 }); // unrelated gift, must survive untouched

        var result = FusionRules.Combine(run, new() { 9003, 9053, 9157 }, "Sinking", 0);

        Assert.Equal(9088, result.Id);
        Assert.Equal(2, run.Gifts.Items.Count); // the unrelated survivor + the fused result
        Assert.DoesNotContain(run.Gifts.Items, g => g.Id is 9003 or 9053 or 9157);
        Assert.Contains(run.Gifts.Items, g => g.Id == 9088);
        Assert.Contains(run.Gifts.Items, g => g.Id == 12345);
    }

    [Fact]
    public void Combine_MissingMaterial_IsIgnored()
    {
        var run = new Run();
        run.Gifts.Items.Add(new EgoGift { Id = 9003 });

        // 9053/9157 aren't owned - the handler's remove-if-found loop just skips them, so this
        // still resolves via the fixed recipe (Rust computes owned AFTER the removals).
        var result = FusionRules.Combine(run, new() { 9003, 9053, 9157 }, "Sinking", 0);

        Assert.Equal(9088, result.Id);
        Assert.DoesNotContain(run.Gifts.Items, g => g.Id == 9003);
    }

    [Fact]
    public void UpgradeGift_ValidGift_BumpsUlAndChargesTierUpgradeCost()
    {
        // 9001 is TIER_2 (see MdShopDataTests): TierUpgradeCost(9001, 1) == 60.
        var run = new Run { Cost = 1000, UsedCost = 0 };
        run.Gifts.Items.Add(new EgoGift { Id = 9001, Ul = 0 });

        var (gift, charged) = FusionRules.UpgradeGift(run, 9001);

        Assert.True(charged);
        Assert.NotNull(gift);
        Assert.Equal(1, gift!.Ul);
        Assert.Equal(940, run.Cost);      // 1000 - 60
        Assert.Equal(60, run.UsedCost);
    }

    [Fact]
    public void UpgradeGift_DiscountGiftOwned_Applies70PercentPrice()
    {
        var run = new Run { Cost = 1000, UsedCost = 0 };
        run.Gifts.Items.Add(new EgoGift { Id = 9001, Ul = 0 });
        run.Gifts.Items.Add(new EgoGift { Id = 9189 }); // "Shop ... Upgrade Cost -30%" discount gift

        var (gift, charged) = FusionRules.UpgradeGift(run, 9001);

        Assert.True(charged);
        Assert.Equal(1, gift!.Ul);
        Assert.Equal(958, run.Cost);     // 1000 - (60*70/100 == 42)
        Assert.Equal(42, run.UsedCost);
    }

    [Fact]
    public void UpgradeGift_UnknownGiftId_ReturnsNullAndDoesNotCharge()
    {
        var run = new Run { Cost = 1000 };
        var (gift, charged) = FusionRules.UpgradeGift(run, 99999999);
        Assert.Null(gift);
        Assert.False(charged);
        Assert.Equal(1000, run.Cost);
    }

    [Fact]
    public void UpgradeGift_NotOwned_ReturnsNullAndDoesNotCharge()
    {
        var run = new Run { Cost = 1000 };
        var (gift, charged) = FusionRules.UpgradeGift(run, 9001);
        Assert.Null(gift);
        Assert.False(charged);
        Assert.Equal(1000, run.Cost);
    }

    [Fact]
    public void UpgradeGift_DesiredUlOutOfRange_ReturnsNullAndDoesNotCharge()
    {
        // 9001's upgradeDataList has 3 entries (ul 0/1/2); already at ul=2 (max), desiredUl=3 is
        // out of range (the bound check is against egoInfo.upgradeDataList.Count, not the
        // 2-entry cost table).
        var run = new Run { Cost = 1000 };
        run.Gifts.Items.Add(new EgoGift { Id = 9001, Ul = 2 });

        var (gift, charged) = FusionRules.UpgradeGift(run, 9001);

        Assert.Null(gift);
        Assert.False(charged);
        Assert.Equal(1000, run.Cost);
        Assert.Equal(2, run.Gifts.Items[0].Ul); // unchanged
    }
}
