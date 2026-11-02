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
    private const long ShopDiscountRefreshGift = 9188;            // "Pre-order Discount"  — Regular Refresh Cost -30%
    private const long ShopDiscountPurchaseGift = 9191;           // "Prestige Card"       — E.G.O Gift Purchase Cost -30%
    private const long ShopDiscountUpgradePersonalityGift = 9190; // "Trial Plan Guide"    — Skill Replacement Cost -30%

    // HP/CM run on the wire's own scale: every unit carries 0..10000 hp and 0..45 cm.
    private const long MaxHp = 10000;
    private const long MaxCm = 45;

    // PurchaseHealMirrorDungeon. Every branch is priced and parameterised by the retail table now
    // (mirrordungeon-07-extreme.json list[0].shop.heal) instead of the reference server's
    // hardcoded amounts: idx 0 = +50% hp / +45 cm on one unit, idx 1 = +20% hp / +15 cm on the
    // whole party, idx 2 = revive one downed unit at full hp. All three cost the table's 100.
    // `usedcost` keeps the reference server's quirk: the idx 0 branch never accumulated it (its
    // DTO hardcodes 100 - see the handler).
    public static void Heal(Run run, long idx, long pid)
    {
        var option = MdShopTable.Heal(idx);
        if (option is null) return; // no such option in the table - the handler answers with a default packet

        var price = Effects.ApplyRaisedMarketPrice(run, option.price);
        run.Cost -= price;
        if (idx != 0) run.UsedCost += price; // cumulative shop-spend this floor.

        var targets = option.isForAll
            ? run.Party
            : run.Party.Where(u => u.PersonalityId == pid).ToList();

        foreach (var u in targets)
        {
            // A revive only touches a unit that is actually down.
            if (option.isRevive && u.CurrentHp > 0) continue;

            if (option.isRevive)
            {
                u.CurrentHp = MaxHp;
            }
            else
            {
                var hpGain = option.isHealHpPercentage ? (long)(MaxHp * option.healHpAmount) : (long)option.healHpAmount;
                if (hpGain != 0) u.CurrentHp = Math.Min(u.CurrentHp + hpGain, MaxHp);
            }

            var cmGain = option.isHealMpPercentage ? (long)(MaxCm * option.healMpAmount) : (long)option.healMpAmount;
            if (cmGain != 0) u.Cm = Math.Min(u.Cm + cmGain, MaxCm);
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
        // Never sell a gift the run already owns (it can arrive by other means - fusion, an
        // event - after the shop rolled), nor a component of a fusion gift it holds (see
        // SharedRules.OwnsGift): nothing is charged and the slot stays as it was.
        if (SharedRules.OwnsGift(run, bought.id)) return new();

        var price = WealthFix(run, Effects.ApplyShopDiscount(run, ShopDiscountPurchaseGift,
            Effects.ApplyRaisedMarketPrice(run, bought.price)));
        run.Cost -= price;
        run.UsedCost += price; // cumulative shop-spend this floor.
        slot.S = 0; // buying flips the slot's s from 1 to 0 (sold out).
        var granted = new EgoGift { Id = bought.id };
        run.Gifts.Items.Add(granted);
        return new() { granted };
    }

    // PurchaseUpgradePersonalityMirrorDungeon: buy one skill replacement for a personality. What
    // the retail capture (md-extreme-run seq36/67/95/96/122/181/235/312) pins down:
    //   * a plain purchase consumes the pid-matching "up" slot, a detected one (buyer picks the
    //     skill back, isDetected) consumes the shop's "upt" targeting slot - seq234 (plain) and
    //     seq235 (detected) are the same shop visit, and each took its own slot;
    //   * neither is gated on a slot being on sale: the client only offers what it can sell, the
    //     server charges and consumes whatever it finds (seq151 buys with the pid slot already 0).
    // Pricing:
    //   * funds, plain, pid slot on sale -> upgradePersonality[idx].price less the -30% gift;
    //   * funds, plain, no pid slot       -> upgradePersonality[idx].price * 2 (as captured);
    //   * funds, detected                 -> upgradePersonality[idx].detectingPrice (2x price) and
    //     never discounted - the discount gift covers the replacement, not the detection;
    //   * useStarlight                    -> upgradePersonality[idx].detectingStarLight out of the
    //     run's starlight (slinfo.scc), no funds and nothing added to usedcost. The super shop's
    //     "upt" slot is the one that offers that choice.
    // upidx APPENDS the bought idx, duplicates included (seq36 [1] -> seq96 [1,2] -> seq122
    // [1,2,1]); es never changes, the client derives the replacement from upidx itself.
    // Returns false when the index is unknown, or when starlight was chosen but cannot be paid.
    public static bool PurchaseUpgradePersonality(Run run, long pid, long idx, bool isDetected, bool useStarlight)
    {
        var opt = OpenLethe.Server.MdUpgradePersonalityCost.ForIndex(idx);
        if (opt is null) return false;

        var upSlot = run.Shop.Slots.FirstOrDefault(s => s.T == "up" && s.Id == pid && s.S == 1);
        var targetSlot = run.Shop.Slots.FirstOrDefault(s => s.T == "upt");

        if (useStarlight)
        {
            if (run.Starlight.Scc < opt.detectingStarLight) return false;
            run.Starlight.Scc -= opt.detectingStarLight;
            if (targetSlot is not null) targetSlot.S = 0;
        }
        else
        {
            long price;
            if (isDetected)
            {
                if (targetSlot is not null) targetSlot.S = 0;
                price = WealthFix(run, Effects.ApplyRaisedMarketPrice(run, opt.detectingPrice));
            }
            else
            {
                var slot = upSlot ?? targetSlot;
                if (slot is not null) slot.S = 0;
                var basePrice = Effects.ApplyRaisedMarketPrice(run, opt.price);
                price = WealthFix(run, upSlot is not null
                    ? Effects.ApplyShopDiscount(run, ShopDiscountUpgradePersonalityGift, basePrice)
                    : basePrice * 2);
            }

            run.Cost -= price;
            run.UsedCost += price; // cumulative shop-spend this floor.
        }

        var unit = run.Party.FirstOrDefault(u => u.PersonalityId == pid);
        if (unit is not null) unit.UpgradeIndices.Add(idx);
        return true;
    }

    // 9800 "Wealth": "Fix all E.G.O Gift Purchase & Enhancement Cost to 1". The client already
    // shows those prices as 1, so the server must charge 1 too (purchase, skill replacement and
    // gift enhancement - FusionRules.UpgradeGift calls this). Refresh and heal are not covered.
    private const long WealthGift = 9800;

    internal static long WealthFix(Run run, long price) =>
        price > 1 && run.Gifts.Items.Any(g => g.Id == WealthGift) ? 1 : price;

    // Verbatim port of SellEgoGiftMirrorDungeon (Handlers/MirrorDungeonShop.cs :223-233), plus
    // Cumulating Starcloud's once-per-shop sale bonus (StartBuffEffects.SellPrice).
    public static void SellEgoGift(Run run, long id)
    {
        var egs = run.Gifts.Items;
        var index = egs.FindIndex(e => e.Id == id);
        if (index < 0) return;
        var info = OpenLethe.Server.MdEgoData.GetById(egs[index].Id);
        if (info is null) return;
        run.Cost += StartBuffEffects.SellPrice(run, info.price / 2);
        egs.RemoveAt(index);
    }

    // RefreshShopEgoGiftsMirrorDungeon. Rerolls every currently-available (s==1) gift slot's id IN
    // PLACE - position, type, and availability preserved; sold-out slots left completely
    // untouched. Only "eg" slots: the "up" slots hold PERSONALITY ids and "upt" is the id-0
    // ticket, so writing gift ids into them corrupted the shop. Two things the reference server
    // dropped are back: the request's `keyword` now reaches the gift picker (it always passed the
    // literal "None"), and the price comes from the tables - shopRefreshCostPerCount * rc, plus
    // shop.refreshShop.shopKeywordRefreshCostAdder for a keyword refresh.
    // customThemes: the account's uploaded packs (see MapNodeRules.EnterNode).
    // A shop visit's first refresh(es) can be free: Star of the Beginning's normal ones (shop.fre,
    // the number left) and Moon Star-shop's keyword ones (shop.fkre). A free refresh charges
    // nothing and does not raise the paid refresh count (rc) the price escalates on.
    public static void RefreshShop(Run run, string? keyword, IReadOnlyList<ThemeStatic>? customThemes = null)
    {
        var giftSlots = run.Shop.Slots.Where(s => s.T == "eg" && s.S == 1).ToList();
        var randomGifts = new MdThemePool(customThemes).SelectRandomShopEgos(
            SharedRules.ThemePackId(run), Math.Min(giftSlots.Count, 30), SharedRules.CurrentFloor(run), keyword,
            SharedRules.OwnedGiftIds(run), StartBuffEffects.ShopHighTierMultiplier(run), run.Idx);

        var giftQueue = new Queue<long>(randomGifts);
        foreach (var slot in giftSlots)
            if (giftQueue.Count > 0) slot.Id = giftQueue.Dequeue();

        // The skill-replacement slots still on sale get a new random sinner too; bought ones keep
        // theirs and "upt" is untouched (md-extreme-run-2 seq284 -> 286: slot 1 rerolls, the sold
        // slot 2 stays 11012). The new picks stay distinct from the shop's other "up" slots.
        var upSlots = run.Shop.Slots.Where(s => s.T == "up" && s.S == 1).ToList();
        var keptPids = run.Shop.Slots.Where(s => s.T == "up" && s.S != 1).Select(s => s.Id);
        var sinnerQueue = new Queue<long>(SharedRules.RandomLivingSinners(run, keptPids));
        foreach (var slot in upSlots)
            if (sinnerQueue.Count > 0) slot.Id = sinnerQueue.Dequeue();

        var keywordRefresh = !string.IsNullOrEmpty(keyword) && keyword != "None";
        if (keywordRefresh && run.Shop.Fkre > 0)
        {
            run.Shop.Fkre -= 1;
            return;
        }
        if (!keywordRefresh && run.Shop.Fre > 0)
        {
            run.Shop.Fre -= 1;
            return;
        }

        run.Shop.Rc += 1;
        // 9188 "Pre-order Discount" says "Shop Regular Refresh Cost -30%", so it covers the
        // regular part only; the keyword adder is a separate fee and stays full price.
        var price = Effects.ApplyShopDiscount(run, ShopDiscountRefreshGift,
            Effects.ApplyRaisedMarketPrice(run, MdCommonTable.RefreshCostPerCount * run.Shop.Rc));
        if (keywordRefresh)
            price += Effects.ApplyRaisedMarketPrice(run, MdShopTable.KeywordRefreshCostAdder);
        run.Cost -= price;
        run.UsedCost += price; // cumulative shop-spend this floor.
    }
}
