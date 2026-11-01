using OpenLethe.Resources;

namespace OpenLethe.Server;

// Retail MD shop economics. Two static tables drive every price and slot count the shop
// endpoints hand out, and neither was read anywhere in the server before this file:
//   * mirrordungeon-07-extreme.json list[0] - shop.heal / shop.upgradePersonality /
//     shop.changeFormation / shop.refreshShop / shop.shopInfoByType, plus restingEvent (the
//     pre-run rest menu) and the egoGift* toggles.
//   * mirror-dungeon-common-data-md7.json - shopRefreshCostPerCount,
//     shopKeywordRefreshWeightMultiplier, maxRestingCountForPersonality, acquireChipPerFloor.
// Everything the shop charges or offers resolves through here so the numbers cannot drift from
// retail again. Field names mirror the JSON verbatim (StaticData binds by name).

// One shop.heal / restingEvent.heal entry. healHpAmount/healMpAmount are fractions of the unit's
// max when the matching isHealXxPercentage flag is set (0.5 = +50% hp), flat points otherwise.
public sealed class MdHealOption
{
    public long index;
    public long price;
    public bool isForAll;
    public bool isRevive;
    public bool isHealHpPercentage;
    public double healHpAmount;
    public bool isHealMpPercentage;
    public double healMpAmount;
}

public sealed class MdChangeFormationTable
{
    public long price;
    public long changeableCount;
}

public sealed class MdRefreshShopTable
{
    public bool isActive;
    public long shopKeywordRefreshCostAdder;
}

// A table row that is just { "isActive": bool } (egoGiftSell / egoGiftUpgrade / egoGiftComposite).
public sealed class MdActiveToggle
{
    public bool isActive;
}

// shop.shopInfoByType: type 0 = the regular shop, type 1 = the super shop (an e==10 node whose
// eid is not 0). egoGiftSlotNum is the shop's gift-slot count (4 / 7), skillSlotNum = how many
// skill-replacement slots it sells (each bound to one of the party's personalities, wire "up"),
// skillTargetingSlotNum = how many extra "you pick the skill" slots it has (wire "upt"; that is
// also the slot that accepts the run's starlight as payment - the super shop's third one),
// changeFormationCount = how many formation swaps one visit allows.
public sealed class MdShopTypeInfo
{
    public long type;
    public long skillSlotNum;
    public long skillTargetingSlotNum;
    public long egoGiftSlotNum;
    public long changeFormationCount;
}

// Not sealed: MdDungeonShopInfo (MdTheme.cs) derives from this and adds the file's
// upgradePersonality slice.
public class MdShopTableInfo
{
    public List<MdHealOption> heal = new();
    public List<MdShopTypeInfo> shopInfoByType = new();
    public MdChangeFormationTable changeFormation = new();
    public MdRefreshShopTable refreshShop = new();
    public MdActiveToggle egoGiftSell = new();
    public MdActiveToggle egoGiftUpgrade = new();
    public MdActiveToggle egoGiftComposite = new();
}

// The pre-run rest menu ("Rest" screen before floor 1): same heal/changeFormation shapes as the
// shop, no gift slots and no shopInfoByType.
public sealed class MdRestingEventTable
{
    public List<MdHealOption> heal = new();
    public MdChangeFormationTable changeFormation = new();
    public MdActiveToggle egoGiftUpgrade = new();
    public MdActiveToggle egoGiftComposite = new();
}

public static class MdShopTable
{
    // Same file (and same list[0] slice) MdUpgradePersonalityCost / MdEnemyBuffPool already read.
    private static readonly Lazy<MdDungeonInfo> Dungeon = new(() =>
        StaticData.GetListFromFile<MdDungeonInfo>("static-data/mirrordungeon/mirrordungeon-07-extreme.json")
            .FirstOrDefault() ?? new MdDungeonInfo());

    public static MdShopTableInfo Shop => Dungeon.Value.shop;
    public static MdRestingEventTable RestingEvent => Dungeon.Value.restingEvent;

    // The heal entry for idx, or null when the table defines no such option (the caller answers
    // with an empty default packet instead of charging, same as before).
    public static MdHealOption? Heal(long idx) => Shop.heal.FirstOrDefault(h => h.index == idx);

    public static MdShopTypeInfo? ShopType(bool superShop) =>
        Shop.shopInfoByType.FirstOrDefault(t => t.type == (superShop ? 1 : 0));

    public static long GiftSlots(bool superShop) => ShopType(superShop)?.egoGiftSlotNum ?? 0;
    public static long UpgradeSlots(bool superShop) => ShopType(superShop)?.skillSlotNum ?? 0;
    public static long UpgradeTicketSlots(bool superShop) => ShopType(superShop)?.skillTargetingSlotNum ?? 0;

    // Price of one formation swap, both on the pre-run screen and in a shop.
    public static long ChangeFormationPrice => Shop.changeFormation.price;

    // Pre-run formation screen allowance (shop.changeFormation.changeableCount: 3).
    public static long ChangeFormationCount => Shop.changeFormation.changeableCount;

    // In-shop allowance for one visit (shopInfoByType.changeFormationCount: 1).
    public static long ShopChangeFormationCount(bool superShop) => ShopType(superShop)?.changeFormationCount ?? 0;

    // Extra cost a keyword refresh adds on top of shopRefreshCostPerCount * rc (75).
    public static long KeywordRefreshCostAdder => Shop.refreshShop.shopKeywordRefreshCostAdder;
}

// mirror-dungeon-common-data-md7.json is one unlisted object per file (StaticData.GetUnlisted):
// only the shop-relevant subset and hiddenBattleInfo are modelled, the rest of the file is
// ignored by name.
public sealed class MdCommonData
{
    public long currentDungeonId;
    public long maxRestingCountForPersonality = 5;
    public long shopRefreshCostPerCount = 15;
    public long shopRefreshChipForAnyCount = 10;
    public List<long> acquireChipPerFloor = new();
    public double shopKeywordRefreshWeightMultiplier = 12.5;
    // Theme packs offered per selection (per difficulty group) and free re-rolls per floor, before
    // the start buffs' ADDITIONAL_FLOOR_THEME_SLOT / ADDITIONAL_FLOOR_THEME_REFRESH.
    public long themePoolNum = 3;
    public long themePoolRecreateCount = 1;
    // Enemy levels a boss-reward pick must add up to for one extra gift pick (4 in md7/md8).
    public long minEnemyLevelBuffForAdditionalEgoGift = 4;
    public MdHiddenBattleInfo? hiddenBattleInfo;
    // Battle-victory cost: a base per encounter type (normal / hard difficulty), scaled by the
    // floor's weight row - see MdCost.BattleVictoryCost.
    public List<MdBattleVictoryCost> originbattlevictorycostvalueList = new();
    public List<MdBattleVictoryCost> originbattlevictorycostvalueListHardLevel = new();
    public List<MdBattleCostWeights> battleCostWeightsByFloor = new();
}

public sealed class MdBattleVictoryCost
{
    public string encounterType = "";
    public long cost;
    // Index into the floor's battleCostWeightsByFloor[*].weights.
    public int battleCostWeightsByFloorIdx;
}

public sealed class MdBattleCostWeights
{
    public long floor;
    public List<double> weights = new();
}

// hiddenBattleInfo: the hidden-boss stages (pool), the first floor they can appear on
// (minFloorCondition) and, per floor, the chance that one of the floor's event nodes turns into a
// hidden battle (probInfo). Floors are 1-based like the client's UI ("floor 11"); the server's
// floor index is 0-based.
public sealed class MdHiddenBattleInfo
{
    public long minFloorCondition;
    public List<long> pool = new();
    public List<MdHiddenBattleProb> probInfo = new();

    public double ProbForFloor(long floorIndex)
    {
        var floor = floorIndex + 1;
        if (floor < minFloorCondition) return 0;
        return probInfo.FirstOrDefault(p => p.floor == floor)?.prob ?? 0;
    }
}

public sealed class MdHiddenBattleProb
{
    public long floor;
    public double prob;
}

public static class MdCommonTable
{
    private static readonly Lazy<List<MdCommonData>> Files = new(() =>
        StaticData.GetUnlisted<MdCommonData>("static-data/mirror-dungeon-common-data"));

    private static readonly Lazy<MdCommonData> Data = new(() => Files.Value.FirstOrDefault() ?? new MdCommonData());

    // The current season's file (currentDungeonId 8 = mirror-dungeon-common-data-md8.json), or the
    // first one when no file names it.
    private static readonly Lazy<MdCommonData> Current = new(() =>
        Files.Value.FirstOrDefault(d => d.currentDungeonId == OpenLethe.Server.MirrorDungeon.Rules.StartRunRules.CurrentDungeonId)
            ?? Data.Value);

    // The current season's hiddenBattleInfo; empty (no hidden battles) when the file has none.
    public static MdHiddenBattleInfo HiddenBattle => Current.Value.hiddenBattleInfo ?? new MdHiddenBattleInfo();

    // The current season's battle-victory cost tables (originbattlevictorycostvalueList /
    // ...ListHardLevel and battleCostWeightsByFloor).
    public static List<MdBattleVictoryCost> VictoryCosts(bool hard) => hard
        ? Current.Value.originbattlevictorycostvalueListHardLevel
        : Current.Value.originbattlevictorycostvalueList;

    public static List<MdBattleCostWeights> CostWeightsByFloor => Current.Value.battleCostWeightsByFloor;

    // Module points charged per shop refresh, multiplied by how many refreshes this floor already
    // had (shop.rc) - the reference server hardcoded the same 15, now it is read.
    public static long RefreshCostPerCount => Data.Value.shopRefreshCostPerCount;

    // Weight multiplier an on-keyword gift gets in a keyword refresh.
    public static double KeywordRefreshWeightMultiplier => Data.Value.shopKeywordRefreshWeightMultiplier;

    // themePoolNum (3) / themePoolRecreateCount (1) of the current season - see StartBuffEffects.
    public static long ThemePoolNum => Current.Value.themePoolNum;
    public static long ThemePoolRecreateCount => Current.Value.themePoolRecreateCount;

    // minEnemyLevelBuffForAdditionalEgoGift of the current season - see
    // RewardResolution.AcquireWithEnemyBuf.
    public static long MinEnemyLevelForAdditionalGift => Current.Value.minEnemyLevelBuffForAdditionalEgoGift;
}
