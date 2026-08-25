using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Model;

namespace OpenLethe.Server.MirrorDungeon.Rules;

// Shop-group rules: heal, ego-gift purchase/sell, personality-upgrade purchase, shop refresh.
// Verbatim ports of Handlers/MirrorDungeonShop.cs PurchaseHeal/PurchaseEgoGift/
// PurchaseUpgradePersonality/SellEgoGift and Handlers/MirrorDungeonRewards.cs
// RefreshShopEgoGiftsMirrorDungeon. See the handler-migration Task 8 brief.
public static class ShopRules
{
    // "Shop ... Cost -30%" discount ego gifts - see the wire ApplyShopDiscount comment
    // (Handlers/MirrorDungeonShop.cs :20-27) for the full derivation. Only the two ids this
    // component's endpoints consult; ShopDiscountUpgradeGift (9189, UpgradeEgoGift) stays in
    // the handler until Task 11 migrates that endpoint.
    private const long ShopDiscountPurchaseGift = 9191;
    private const long ShopDiscountUpgradePersonalityGift = 9190;

    // Verbatim port of PurchaseHealMirrorDungeon (Handlers/MirrorDungeonShop.cs :62-108).
    // idx==1: rest/heal-all - +2000 hp (cap 10000) / +15 cm (cap 45) per unit, flat 100 cost,
    // cumulative usedcost. idx==0: unexercised bandaid path - flat +100 hp / +30 cm, no cap, no
    // running usedcost (the handler hardcodes the DTO's usedcost=100 for this branch instead of
    // reading persisted state - reproduced there, not here). Any other idx is the "no matching
    // branch in Rust" case - the caller skips this method entirely for that (see the handler).
    public static void Heal(Run run, long idx, long pid)
    {
        switch (idx)
        {
            case 0:
                run.Cost -= 100;
                var unit = run.Party.FirstOrDefault(u => u.PersonalityId == pid);
                if (unit is not null) { unit.CurrentHp += 100; unit.Cm += 30; }
                break;
            case 1:
                run.Cost -= 100;
                run.UsedCost += 100; // cumulative shop-spend this floor.
                foreach (var u in run.Party)
                {
                    u.CurrentHp = Math.Min(u.CurrentHp + 2000, 10000);
                    u.Cm = Math.Min(u.Cm + 15, 45);
                }
                break;
        }
    }

    // Verbatim port of PurchaseEgoGiftMirrorDungeon (Handlers/MirrorDungeonShop.cs :124-143).
    // idx indexes ONLY the "eg" shop slots (skips leading "up"/"upt" slots). Returns the granted
    // gift (0 or 1 entries); the DTO's full egogifts list is still read off ToWire(run) by the
    // caller, as it was off save.currentInfo.egs before.
    public static List<EgoGift> PurchaseEgoGift(Run run, long idx)
    {
        var egSlots = run.Shop.Slots.Where(s => s.T == "eg").ToList();
        if (idx < 0 || idx >= egSlots.Count) return new();

        var slot = egSlots[(int)idx];
        var bought = OpenLethe.Server.MdEgoData.GetById(slot.Id);
        if (bought is null) return new();

        var price = Effects.ApplyShopDiscount(run, ShopDiscountPurchaseGift, bought.price);
        run.Cost -= price;
        run.UsedCost += price; // cumulative shop-spend this floor.
        slot.S = 0; // buying flips the slot's s from 1 to 0 (sold out).
        var granted = new EgoGift { Id = bought.id };
        run.Gifts.Items.Add(granted);
        return new() { granted };
    }

    // Verbatim port of PurchaseUpgradePersonalityMirrorDungeon (Handlers/MirrorDungeonShop.cs
    // :167-199). Consumes the pid-matching "up" slot if still available, else falls back to the
    // universal "upt" ticket at 2x price (the -30% gift discounts only the up-slot branch).
    public static void PurchaseUpgradePersonality(Run run, long pid, long idx)
    {
        var opt = OpenLethe.Server.MdUpgradePersonalityCost.ForIndex(idx);
        if (opt is null) return;

        var upSlot = run.Shop.Slots.FirstOrDefault(s => s.T == "up" && s.Id == pid && s.S == 1);
        var slot = upSlot ?? run.Shop.Slots.FirstOrDefault(s => s.T == "upt");
        if (slot is not null) slot.S = 0;

        var price = upSlot is not null
            ? Effects.ApplyShopDiscount(run, ShopDiscountUpgradePersonalityGift, opt.price)
            : opt.price * 2;
        run.Cost -= price;
        run.UsedCost += price; // cumulative shop-spend this floor.

        var unit = run.Party.FirstOrDefault(u => u.PersonalityId == pid);
        if (unit is not null) unit.UpgradeIndices.Add(idx);
    }

    // Verbatim port of SellEgoGiftMirrorDungeon (Handlers/MirrorDungeonShop.cs :223-233).
    public static void SellEgoGift(Run run, long id)
    {
        var egs = run.Gifts.Items;
        var index = egs.FindIndex(e => e.Id == id);
        if (index < 0) return;
        var info = OpenLethe.Server.MdEgoData.GetById(egs[index].Id);
        if (info is null) return;
        run.Cost += info.price / 2;
        egs.RemoveAt(index);
    }

    // Verbatim port of RefreshShopEgoGiftsMirrorDungeon (Handlers/MirrorDungeonRewards.cs
    // :97-127). Rerolls every currently-available (s==1) slot's id IN PLACE - position, type,
    // and availability preserved; sold-out slots left completely untouched. `keyword` is always
    // the literal "None" across both captures (never a real gift keyword, and no domain field
    // carries it), so it's hardcoded rather than threaded through as a param - the RNG content
    // either way lands in slots[*].id, which the replay masks.
    public static void RefreshShop(Run run)
    {
        var count = Math.Min(run.Shop.Slots.Count(s => s.S == 1), 30);
        var randomGifts = new MdThemePool().SelectRandomShopEgos(
            SharedRules.ThemePackId(run), count, SharedRules.CurrentFloor(run), "None");

        var giftQueue = new Queue<long>(randomGifts);
        foreach (var slot in run.Shop.Slots)
            if (slot.S == 1 && giftQueue.Count > 0) slot.Id = giftQueue.Dequeue();

        run.Shop.Rc += 1;
        var price = 15 * run.Shop.Rc;
        run.Cost -= price;
        run.UsedCost += price; // cumulative shop-spend this floor.
    }
}
