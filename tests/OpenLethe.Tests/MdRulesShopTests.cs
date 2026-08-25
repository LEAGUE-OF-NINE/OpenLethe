using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.MirrorDungeon.Rules;

namespace OpenLethe.Tests;

// Component tests for the Task 8 shop-group rules: ShopRules (Run-mutating). Mirrors the
// pricing/indexing assertions documented in the handler-migration ledger (task-12/PUP entries)
// and the wire-typed handler bodies they replace, translated onto the domain Run/EgoGift/
// ShopState types. Structure/behavior, not golden RNG - RefreshShop's rerolled ids are random
// in both the original and the port, so only shape (T/S preserved, price, position) is asserted.
public class MdRulesShopTests
{
    [Fact]
    public void Heal_Idx1_FullHealsAllPartyCapsAndChargesCumulativeCost()
    {
        var run = new Run { Cost = 1000, UsedCost = 50 };
        run.Party.Add(new PartyUnit { PersonalityId = 1, CurrentHp = 9000, Cm = 20 });  // 9000+2000 -> cap 10000, 20+15=35
        run.Party.Add(new PartyUnit { PersonalityId = 2, CurrentHp = 8500, Cm = 40 });  // 8500+2000 -> cap 10000, 40+15 -> cap 45

        ShopRules.Heal(run, 1, 0);

        Assert.Equal(10000, run.Party[0].CurrentHp);
        Assert.Equal(35, run.Party[0].Cm);
        Assert.Equal(10000, run.Party[1].CurrentHp);
        Assert.Equal(45, run.Party[1].Cm);
        Assert.Equal(900, run.Cost);
        Assert.Equal(150, run.UsedCost); // cumulative: 50 + 100
    }

    [Fact]
    public void Heal_Idx0_SingleUnitBandaid_NoCapAndNoUsedCostTracking()
    {
        var run = new Run { Cost = 500, UsedCost = 0 };
        run.Party.Add(new PartyUnit { PersonalityId = 5, CurrentHp = 100, Cm = 10 });

        ShopRules.Heal(run, 0, 5);

        Assert.Equal(200, run.Party[0].CurrentHp);
        Assert.Equal(40, run.Party[0].Cm);
        Assert.Equal(400, run.Cost);
        Assert.Equal(0, run.UsedCost); // idx==0 doesn't persist a running usedcost (matches the handler)
    }

    [Fact]
    public void Heal_Idx0_UnknownPid_StillChargesCost()
    {
        var run = new Run { Cost = 500 };
        ShopRules.Heal(run, 0, 999);
        Assert.Equal(400, run.Cost);
    }

    [Fact]
    public void PurchaseEgoGift_IndexesOnlyEgSlots_SkippingLeadingUpSlot()
    {
        var run = new Run { Cost = 1000, UsedCost = 0 };
        run.Shop.Slots.Add(new ShopSlotState { T = "up", Id = 11202, S = 1 });
        run.Shop.Slots.Add(new ShopSlotState { T = "eg", Id = 9001, S = 1 }); // price 198 (static data)
        run.Shop.Slots.Add(new ShopSlotState { T = "eg", Id = 9001, S = 1 });

        var granted = ShopRules.PurchaseEgoGift(run, 0);

        Assert.Equal(1, run.Shop.Slots[0].S); // leading "up" slot untouched
        Assert.Equal(0, run.Shop.Slots[1].S); // 1st eg slot (not slots[0]) sold out
        Assert.Equal(1, run.Shop.Slots[2].S); // 2nd eg slot untouched
        Assert.Equal(802, run.Cost); // 1000 - 198
        Assert.Equal(198, run.UsedCost);
        Assert.Single(granted);
        Assert.Equal(9001, granted[0].Id);
        Assert.Contains(run.Gifts.Items, g => g.Id == 9001);
    }

    [Fact]
    public void PurchaseEgoGift_UsedCostIsCumulativeAcrossPurchases()
    {
        var run = new Run { Cost = 1000, UsedCost = 0 };
        run.Shop.Slots.Add(new ShopSlotState { T = "eg", Id = 9001, S = 1 });
        run.Shop.Slots.Add(new ShopSlotState { T = "eg", Id = 9001, S = 1 });

        ShopRules.PurchaseEgoGift(run, 0);
        ShopRules.PurchaseEgoGift(run, 1);

        Assert.Equal(396, run.UsedCost); // 198 + 198
        Assert.Equal(604, run.Cost);     // 1000 - 396
        Assert.Equal(2, run.Gifts.Items.Count);
    }

    [Fact]
    public void PurchaseEgoGift_IdxOutOfEgSlotRange_IsNoOp()
    {
        var run = new Run { Cost = 1000 };
        run.Shop.Slots.Add(new ShopSlotState { T = "eg", Id = 9001, S = 1 });

        var granted = ShopRules.PurchaseEgoGift(run, 5);

        Assert.Empty(granted);
        Assert.Equal(1000, run.Cost);
        Assert.Empty(run.Gifts.Items);
    }

    [Fact]
    public void PurchaseUpgradePersonality_ConsumesMatchingUpSlot_At1xPrice()
    {
        var run = new Run { Cost = 1000, UsedCost = 0 };
        run.Shop.Slots.Add(new ShopSlotState { T = "up", Id = 77, S = 1 });
        run.Shop.Slots.Add(new ShopSlotState { T = "upt", Id = 0, S = 1 });
        run.Party.Add(new PartyUnit { PersonalityId = 77 });

        ShopRules.PurchaseUpgradePersonality(run, 77, 0); // index 0 -> price 45 (static data)

        Assert.Equal(0, run.Shop.Slots[0].S); // "up" slot consumed
        Assert.Equal(1, run.Shop.Slots[1].S); // "upt" left untouched
        Assert.Equal(955, run.Cost); // 1000 - 45
        Assert.Equal(45, run.UsedCost);
        Assert.Contains(0L, run.Party[0].UpgradeIndices);
    }

    [Fact]
    public void PurchaseUpgradePersonality_FallsBackToUniversalTicket_At2xPrice_WhenUpSlotSpent()
    {
        var run = new Run { Cost = 1000, UsedCost = 0 };
        run.Shop.Slots.Add(new ShopSlotState { T = "up", Id = 77, S = 0 }); // already spent
        run.Shop.Slots.Add(new ShopSlotState { T = "upt", Id = 0, S = 1 });
        run.Party.Add(new PartyUnit { PersonalityId = 77 });

        ShopRules.PurchaseUpgradePersonality(run, 77, 1); // index 1 -> price 75

        Assert.Equal(0, run.Shop.Slots[0].S);
        Assert.Equal(0, run.Shop.Slots[1].S); // "upt" consumed
        Assert.Equal(850, run.Cost); // 1000 - 150 (75*2)
        Assert.Equal(150, run.UsedCost);
        Assert.Contains(1L, run.Party[0].UpgradeIndices);
    }

    [Fact]
    public void PurchaseUpgradePersonality_UnknownIdx_IsNoOp()
    {
        var run = new Run { Cost = 1000 };
        run.Shop.Slots.Add(new ShopSlotState { T = "upt", Id = 0, S = 1 });

        ShopRules.PurchaseUpgradePersonality(run, 1, 999999);

        Assert.Equal(1000, run.Cost);
        Assert.Equal(1, run.Shop.Slots[0].S);
    }

    [Fact]
    public void SellEgoGift_RefundsHalfPriceAndRemoves()
    {
        var run = new Run { Cost = 100 };
        run.Gifts.Items.Add(new EgoGift { Id = 9001 }); // price 198

        ShopRules.SellEgoGift(run, 9001);

        Assert.Equal(199, run.Cost); // 100 + 198/2
        Assert.Empty(run.Gifts.Items);
    }

    [Fact]
    public void SellEgoGift_UnknownId_IsNoOp()
    {
        var run = new Run { Cost = 100 };
        run.Gifts.Items.Add(new EgoGift { Id = 9001 });

        ShopRules.SellEgoGift(run, 12345);

        Assert.Equal(100, run.Cost);
        Assert.Single(run.Gifts.Items);
    }

    [Fact]
    public void RefreshShop_PricesByRcAndPreservesPositionTypeAvailability()
    {
        var run = new Run { Cost = 1000, UsedCost = 0 };
        run.Shop.Rc = 0;
        run.Shop.Slots.Add(new ShopSlotState { T = "eg", Id = 1, S = 1 });
        run.Shop.Slots.Add(new ShopSlotState { T = "eg", Id = 2, S = 0 }); // sold out, must stay untouched
        run.Shop.Slots.Add(new ShopSlotState { T = "up", Id = 3, S = 1 });

        ShopRules.RefreshShop(run);

        Assert.Equal(1, run.Shop.Rc);
        Assert.Equal(985, run.Cost);     // 1000 - 15*1
        Assert.Equal(15, run.UsedCost);

        // position/type/availability preserved regardless of type; only sold-out (s==0) ids are untouched.
        Assert.Equal("eg", run.Shop.Slots[0].T);
        Assert.Equal(1, run.Shop.Slots[0].S);
        Assert.Equal("eg", run.Shop.Slots[1].T);
        Assert.Equal(0, run.Shop.Slots[1].S);
        Assert.Equal(2, run.Shop.Slots[1].Id); // untouched sold-out slot
        Assert.Equal("up", run.Shop.Slots[2].T);
        Assert.Equal(1, run.Shop.Slots[2].S);
    }

    [Fact]
    public void RefreshShop_CumulativeRcPricing()
    {
        var run = new Run { Cost = 1000 };
        run.Shop.Slots.Add(new ShopSlotState { T = "eg", Id = 1, S = 1 });

        ShopRules.RefreshShop(run);
        ShopRules.RefreshShop(run);

        Assert.Equal(2, run.Shop.Rc);
        Assert.Equal(1000 - 15 - 30, run.Cost); // 1st refresh 15*1, 2nd 15*2
    }
}
