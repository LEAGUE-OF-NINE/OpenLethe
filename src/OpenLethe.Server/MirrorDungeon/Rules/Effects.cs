using System.Linq;
using OpenLethe.Server.MirrorDungeon.Model;

namespace OpenLethe.Server.MirrorDungeon.Rules;

// Relocation home for the hardcoded effect constants the old wire handlers hid inline.
// Every table/value here is copied VERBATIM from its wire twin - no behavior change, only a
// Run/scalar signature. See the handler-migration Task 2 brief for the source file:line map.
public static class Effects
{
    // Verbatim port of Handlers/MirrorDungeonMap.cs HiddenGiftLevelBump (:107-113).
    public static long HiddenGiftLevelBump(long giftId) => giftId switch
    {
        993003 => 2,
        993005 => 3,
        _ => 0,
    };

    // Verbatim port of Handlers/MirrorDungeonShop.cs ApplyShopDiscount (:29-31), retargeted to Run.
    public static long ApplyShopDiscount(Run run, long discountGiftId, long price) =>
        run.Gifts.Items.Any(g => g.Id == discountGiftId) ? price * 70 / 100 : price;

    // Verbatim port of Handlers/MirrorDungeon.cs StartingCost (:189-190).
    public static long StartingCost(long dungeonId) => dungeonId == 7 ? 200 : 500;

    // Verbatim port of Handlers/MirrorDungeonMap.cs IsHiddenConfirmedGift (:156). Confirmed-gift
    // ids split into "normal" (9xxx, deferred to ExitMapNode's e==3 realization) and "hidden"
    // (993xxx level-cap gifts, land immediately at UpdateMapNode).
    public static bool IsHiddenConfirmedGift(long id) => id >= 990000;
}
