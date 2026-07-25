using System.Linq;
using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Model;

namespace OpenLethe.Server.MirrorDungeon.Rules;

// Combine/upgrade-gift rules. DELEGATES the recipe/roll to the shared OpenLethe.Server.MdEgoFusion
// reader (used by Story-MD too - not moved, not modified). Verbatim ports of
// Handlers/MirrorDungeonRewards.cs CombineEgoGiftMirrorDungeon and
// Handlers/MirrorDungeonShop.cs UpgradeEgoGiftMirrorDungeon. See the handler-migration Task 11 brief.
public static class FusionRules
{
    // "Shop ... Upgrade Cost -30%" discount gift - see ShopRules' identical comment for the
    // sibling discount ids (9191/9190). Stayed in the handler through Task 8; this is its move.
    private const long ShopDiscountUpgradeGift = 9189;

    // Verbatim port of CombineEgoGiftMirrorDungeon (Handlers/MirrorDungeonRewards.cs :35-74).
    // `isOrigin` mirrors the request DTO 1:1 - unused here, same as the handler it replaces
    // (Rust never reads it either).
    public static EgoGift Combine(Run run, System.Collections.Generic.List<long> materialEgoGiftIds, string keyword, long isOrigin)
    {
        // Remove the component egos - first match each, misses are ignored.
        foreach (var materialId in materialEgoGiftIds)
        {
            var pos = run.Gifts.Items.FindIndex(e => e.Id == materialId);
            if (pos >= 0) run.Gifts.Items.RemoveAt(pos);
        }

        var isSuperShop = SharedRules.IsSuperShop(run) ?? false;
        var fused = OpenLethe.Server.MdEgoFusion.FuseGift(keyword, isSuperShop, materialEgoGiftIds);
        // Rust computes owned AFTER the removals.
        var owned = run.Gifts.Items.Select(e => e.Id).ToList();

        EgoGift result;
        if (fused.FixedGiftId is { } fixedId)
        {
            result = new EgoGift { Id = fixedId };
        }
        else
        {
            var themePool = new MdThemePool();
            var candidates = themePool
                .ShopEgoGiftsPool(SharedRules.ThemePackId(run))
                .Where(g => OpenLethe.Server.MdEgoData.DetermineEgoTier(g) == fused.Tier && !owned.Contains(g))
                .ToList();

            // Rust compares Option<String> == Option<String>: when the keyword roll failed
            // (null), gifts with a null keyword match. Same semantics here.
            var keywordMatches = candidates
                .Where(g => OpenLethe.Server.MdEgoData.GetById(g)?.keyword == fused.Keyword)
                .ToList();
            if (keywordMatches.Count > 0) candidates = keywordMatches;

            // ponytail: 9034 is Rust's hard-coded fallback when nothing is eligible.
            result = new EgoGift
            {
                Id = candidates.Count > 0 ? candidates[System.Random.Shared.Next(candidates.Count)] : 9034,
            };
        }

        run.Gifts.Items.Add(result);
        return result;
    }

    // Verbatim port of UpgradeEgoGiftMirrorDungeon (Handlers/MirrorDungeonShop.cs :175-191).
    // Returns (null, false) for every "500" condition the handler used to return directly
    // (unknown gift id, not owned, desired upgrade level out of range) - the caller maps that
    // to Results.StatusCode(500). Charged tells the caller whether to echo the cumulative
    // run.UsedCost or the handler's old "usedCost stays 0" no-charge case (egoInfo has no
    // upgradeDataList - a valid, chargeless success).
    public static (EgoGift? Gift, bool Charged) UpgradeGift(Run run, long egoGiftId)
    {
        var egoInfo = OpenLethe.Server.MdEgoData.GetById(egoGiftId);
        if (egoInfo is null) return (null, false);
        var ego = run.Gifts.Items.FirstOrDefault(e => e.Id == egoGiftId);
        if (ego is null) return (null, false);

        if (egoInfo.upgradeDataList is not null)
        {
            var desiredUl = ego.Ul + 1;
            if (desiredUl < 0 || desiredUl >= egoInfo.upgradeDataList.Count) return (null, false);
            var price = Effects.ApplyShopDiscount(run, ShopDiscountUpgradeGift,
                OpenLethe.Server.MdEgoData.TierUpgradeCost(egoGiftId, desiredUl));
            run.Cost -= price;
            run.UsedCost += price; // cumulative shop-spend this floor.
            ego.Ul = desiredUl;
            return (ego, true);
        }

        return (ego, false);
    }
}
