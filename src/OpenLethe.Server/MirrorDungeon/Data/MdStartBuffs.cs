using OpenLethe.Resources;

namespace OpenLethe.Server.MirrorDungeon.Data;

// Start-of-run buff catalog + point economy. Sourced from the current season's
// mirrordungeon-start-buffs/mirrordungeon-start-buffs-NN.json list[0]: buffPoint.basePoint
// (60) is the spendable budget, remainPointToCostMultiplier (5) converts spent buff points
// into the in-game `cost` currency EnableStartBuff charges.
public sealed class MdStartBuffPoint
{
    public long basePoint;
    public long remainPointToCostMultiplier;
}

public sealed class MdStartBuffEffect
{
    public string type = "";
    public long value;
    public long value2;
}

// One buff at one enhancement level. The ten buffs are ids 100-109 (level 1); their two
// enhancements are 200-209 (level 2) and 300-309 (level 3), each carrying its own full effect
// list and sharing the base buff's baseId.
public sealed class MdStartBuffEntry
{
    public long id;
    public long level;
    public long baseId;
    public long cost;
    public List<MdStartBuffEffect> effects = new();
}

// Personality level-cap offsets contributed by a set of enabled start buffs, read from each
// buff's effects[]. Replaces the old hardcoded base-3 / cap-8 sbmlos:
//   sbmlos = EnterFirstFloor + min(floorClears, ClearCap)*PerClear
//          + min(snft*PerNewTheme, NewThemeCap) + (floorClears >= 6 ? EnterSixthFloor : 0)
// (ENTER_1ST_FLOOR seeds the floor-1 cap, CLEAR_FLOOR adds `value` per clear up to `value2`
// clears, SELECT_NEW_FLOOR_THEME adds `value` per new-theme floor, ENTER_6TH_FLOOR adds its
// `value` once the run is on floor 6 - the SelectThemeFloor of floor index 5 applies it, the
// sixth clear's absolute recompute keeps it).
public readonly record struct MdLevelOffsets(long EnterFirstFloor, long PerClear, long ClearCap, long PerNewTheme,
    long EnterSixthFloor = 0)
{
    // "When choosing a new Theme Pack for the first time, all Identities gain +3 Level(s) (max 9)"
    // (EN_DungeonStartBuffs ADDITIONAL_PERSONALITY_LEVEL_ON_SELECT_NEW_FLOOR_THEME). The cap is
    // only in the text; the effect row carries no value2 for it.
    public const long NewThemeCap = 9;

    public long NewThemeBonus(long snft) => Math.Min(snft * PerNewTheme, NewThemeCap);

    public long SbmlosAt(long floorClears, long snft) =>
        EnterFirstFloor + Math.Min(floorClears, ClearCap) * PerClear + NewThemeBonus(snft)
        + (floorClears >= 6 ? EnterSixthFloor : 0);
}

public sealed class MdStartBuffDungeon
{
    public long dungeonId;
    public MdStartBuffPoint buffPoint = new();
    public List<MdStartBuffEntry> buffs = new();
}

// The effect types the start buffs carry (mirrordungeon-start-buffs-NN.json effects[].type),
// with their EN text (EN_DungeonStartBuffs*.json). The combat ones (MaxSpeedUpAdder,
// GiveBuffOnRoundStart, GiveBuffOnRSOnThemePackUnlockCount) are applied by the client's battle
// from the enabled list; everything here is the server's to apply.
public static class StartBuffEffect
{
    public const string StartCost = "ADDITIONAL_START_COST";                                   // Starting Cost +{0}
    public const string ThemeSlot = "ADDITIONAL_FLOOR_THEME_SLOT";                             // Theme Pack Selection +{0}
    public const string ThemeRefresh = "ADDITIONAL_FLOOR_THEME_REFRESH";                       // Theme Pack Refresh Chance +{0}
    public const string ShopGiftSlot = "ADDITIONAL_SHOP_EGO_GIFT_SLOT";                        // Shop E.G.O Gift Choice +{0}
    public const string FreeRefresh = "FREE_SHOP_REFRESH_EGO_GIFT_ONCE";                       // {0} Free normal Refresh(es) for Shops
    public const string FreeKeywordRefresh = "FREE_SHOP_KEYWORD_REFRESH_EGO_GIFT_ONCE";        // {0} Free Keyword Refresh(es) for Shops
    public const string CostRateOnClearFloor = "ADDITIONAL_COST_RATE_ON_CLEAR_FLOOR";          // next floor: Cost balance +{0}% (max {1})
    public const string SellPriceRateOnce = "ADDITIONAL_SHOP_SELL_EGO_GIFT_COST_RATE_ONCE";    // E.G.O Gift sale price +{0}% (once per Shop)
    public const string StartGiftSelect = "ADDITIONAL_START_EGO_GIFT_SELECT";                  // Starting E.G.O Gift Choice +{0}
    public const string StarlightCardCost = "ACQUIRE_COST_AFTER_SELECT_STARLIGHT_BATTLE_REWARD"; // Starlight reward card: Cost +{0}
    public const string WinBattleCostRate = "ADDITIONAL_COST_AFTER_WIN_BATTLE";                // Encounter Reward Cost +{0}%
    public const string ShopHighTierChance = "ADDITIONAL_SHOP_HIGH_TIER_EGO_GIFT_CHANCE";      // greater chance for high Tier shop gifts
    public const string TierTotalCostOnShop = "ACQUIRE_COST_BY_EGOGIFT_TIER_TOTAL_ON_ENTER_SHOP"; // shop entry: owned Tier sum as Cost (max {1})
    public const string EnhancedShopProb = "ADDITIONAL_ENHANCED_SHOP_APPEARANCE_PROB";         // +{0}% chance for Super Shop
    public const string Tier1GiftsOnFloor1 = "ACQUIRE_1_TIER_RANDOM_EGO_GIFT_ON_ENTER_1ST_FLOOR"; // floor 1: {0} random Tier 1 gift(s)
    public const string LevelOnEnterFloor1 = "ADDITIONAL_PERSONALITY_LEVEL_ON_ENTER_1ST_FLOOR";
    public const string LevelOnEnterFloor6 = "ADDITIONAL_PERSONALITY_LEVEL_ON_ENTER_6TH_FLOOR";
    public const string LevelOnClearFloor = "ADDITIONAL_PERSONALITY_LEVEL_ON_CLEAR_FLOOR";
    public const string LevelOnNewTheme = "ADDITIONAL_PERSONALITY_LEVEL_ON_SELECT_NEW_FLOOR_THEME";
    public const string VestigeOnEnterShop = "ACQUIRE_3_TIER_RANDOM_MATERIAL_EGO_GIFT_ON_ENTER_SHOP"; // shop entry: 1 Vestige
    public const string Tier3GiftOnFloor3 = "ACQUIRE_3_TIER_RANDOM_EGO_GIFT_ON_ENTER_3RD_FLOOR";       // floor 3: random Tier 3
    public const string Tier3KeywordGiftOnFloor3And5 = "ACQUIRE_3_TIER_RANDOM_EGO_GIFT_ON_ENTER_3F_5F"; // floor 3/5: Tier 3 of the keyword
    public const string Tier4KeywordGiftOnFloor357 = "ACQUIRE_4_TIER_RANDOM_EGO_GIFT_ON_ENTER_3F_5F_7F"; // floor 3/5/7: Tier 4 of the keyword
    public const string LowerTierGiftOnCombine = "ACQUIRE_LOWER_TIER_RANDOM_EGO_GIFT_ON_COMBINE";   // fusion: extra gift {0} Tier(s) lower (once per Shop)
    public const string ChosenTier3KeywordGifts = "ACQUIRE_3_TIER_RANDOM_KEYWORD_EGO_GIFT_ON_ENTER_1ST_FLOOR"; // entry: {0} Tier 3 of the keyword, your choice
    public const string Vestige1OnFloor1 = "ACQUIRE_1_TIER_MATERIAL_EGO_GIFT_ON_ENTER_1ST_FLOOR";      // Dark Vestige
    public const string Vestige12OnFloor1 = "ACQUIRE_12_TIER_MATERIAL_EGO_GIFT_ON_ENTER_1ST_FLOOR";    // Dark + Faint Vestige
    public const string Vestige123OnFloor1 = "ACQUIRE_123_TIER_MATERIAL_EGO_GIFT_ON_ENTER_1ST_FLOOR";  // Dark + Faint + Twinkling Vestige
}

public static class MdStartBuffs
{
    // Every season's catalog; the current season's (dungeonId 8) is the one in play. The 07 and
    // 08 files carry the same buffs - only dungeonId differs.
    private static readonly Lazy<MdStartBuffDungeon?> Data = new(() =>
    {
        var all = StaticData.GetList<MdStartBuffDungeon>("static-data/mirrordungeon-start-buffs");
        return all.FirstOrDefault(d => d.dungeonId == OpenLethe.Server.MirrorDungeon.Rules.StartRunRules.CurrentDungeonId)
            ?? all.OrderByDescending(d => d.dungeonId).FirstOrDefault();
    });

    public static long BasePoint => Data.Value?.buffPoint.basePoint ?? 0;

    public static MdStartBuffEntry? Get(long id) => Data.Value?.buffs.FirstOrDefault(b => b.id == id);

    // The buffs that are actually in effect: each base buff once, at the highest enhancement level
    // among the enabled ids (the client sends the one id of the level it uses - 206 for "Favor of
    // the Nebulae+" - but a list naming two levels of one buff must not stack both). Unknown ids
    // are dropped.
    public static List<MdStartBuffEntry> Effective(IEnumerable<long> buffIds) =>
        buffIds.Distinct()
            .Select(Get)
            .OfType<MdStartBuffEntry>()
            .GroupBy(b => b.baseId == 0 ? b.id : b.baseId)
            .Select(g => g.OrderByDescending(b => b.level).First())
            .ToList();

    public static IEnumerable<MdStartBuffEffect> EffectsOf(IEnumerable<long> buffIds, string type) =>
        Effective(buffIds).SelectMany(b => b.effects).Where(e => e.type == type);

    public static long Sum(IEnumerable<long> buffIds, string type) => EffectsOf(buffIds, type).Sum(e => e.value);

    // Sum of the picked buffs' raw point cost (NOT yet converted to the in-game `cost`
    // currency). Capture-verified: buffs [106,102] cost 40+20=60 (== BasePoint exactly).
    public static long RawSpend(IEnumerable<long> buffIds)
    {
        var d = Data.Value;
        return d is null ? 0 : buffIds.Sum(id => d.buffs.FirstOrDefault(b => b.id == id)?.cost ?? 0);
    }

    // Converts leftover buff points into the in-game `cost` currency.
    public static long PointToCostMultiplier => Data.Value?.buffPoint.remainPointToCostMultiplier ?? 0;

    // ADDITIONAL_START_COST added to the run's `cost` budget at AcquireStart, for a run that never
    // stored its picks (the replay's injected retail saves carry no enabledStartbufIds). The
    // AcquireStart request reveals them: each buff carrying ADDITIONAL_START_EGO_GIFT_SELECT lets
    // the player pick one EXTRA starting ego gift, so the count of extra selections
    // (selectedEgoGiftIds - 1) identifies the cost buffs. Capture-verified: run-1 picks 1 gift
    // (0 extra -> +0), run-2 picks 2 (1 extra -> buff103's +400).
    // ponytail: this is a RECONSTRUCTION - it assumes every ADDITIONAL_START_EGO_GIFT_SELECT buff
    // also carries ADDITIONAL_START_COST (true for 103, not for 109). Runs that stored their picks
    // use StartCostOf instead.
    public static long AdditionalStartCost(int extraSelects)
    {
        var d = Data.Value;
        if (d is null || extraSelects <= 0) return 0;
        return d.buffs
            .Where(b => b.level <= 1 && b.effects.Any(e => e.type == StartBuffEffect.StartGiftSelect))
            .Take(extraSelects)
            .Sum(b => b.effects.Where(e => e.type == StartBuffEffect.StartCost).Sum(e => e.value));
    }

    // ADDITIONAL_START_COST carried by the enabled buffs themselves (100: +150/+250/+250,
    // 103: +400/+500/+700).
    public static long StartCostOf(IEnumerable<long> buffIds) => Sum(buffIds, StartBuffEffect.StartCost);

    // Reads the personality level-cap offsets from the enabled buffs' effects[] (any other effect
    // is ignored). Verified against buffs [106,102]: ENTER_1ST_FLOOR 3, CLEAR_FLOOR value 1 /
    // value2 5, and SELECT_NEW_FLOOR_THEME 3 -> (3, 1, 5, 3), i.e. base 3, +1/clear capped at +5
    // (=8), +3/new theme floor.
    public static MdLevelOffsets LevelOffsets(IEnumerable<long> buffIds)
    {
        long enter = 0, perClear = 0, clearCap = 0, perNewTheme = 0, enterSixth = 0;
        foreach (var b in Effective(buffIds))
            foreach (var e in b.effects)
                switch (e.type)
                {
                    case StartBuffEffect.LevelOnEnterFloor1: enter += e.value; break;
                    case StartBuffEffect.LevelOnEnterFloor6: enterSixth += e.value; break;
                    case StartBuffEffect.LevelOnClearFloor: perClear += e.value; clearCap += e.value2; break;
                    case StartBuffEffect.LevelOnNewTheme: perNewTheme += e.value; break;
                }
        return new MdLevelOffsets(enter, perClear, clearCap, perNewTheme, enterSixth);
    }
}
