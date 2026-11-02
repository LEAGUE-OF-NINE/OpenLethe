using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Model;

namespace OpenLethe.Server.MirrorDungeon.Rules;

// The server-side effects of the run's enabled start buffs ("Grace of the Star"), read from the
// buffs' effects[] (mirrordungeon-start-buffs-NN.json) with the EN text as the rule
// (EN_DungeonStartBuffs*.json). Every buff is one of ten base buffs at enhancement level 1-3
// (ids 1xx/2xx/3xx); MdStartBuffs.Effective keeps one level per buff. The rules call in here at
// the moment each effect fires; the combat effects (Starlight Guidance's Max Speed / Clash Power /
// Damage Up / Protection, Interstellar Travel's compendium Clash Power) are the client battle's.
public static class StartBuffEffects
{
    // A run that never stored its picks - the replay's injected retail saves carry no
    // enabledStartbufIds - falls back to the pair both captured runs enabled, which keeps their
    // goldens (4-pack offers, 3 re-rolls, sbmlos 3 + 1/clear) intact. A run whose player enabled
    // nothing has StartBuffsChosen set and gets no effects.
    private static readonly long[] UnstoredFallback = { 106, 102 };

    public static IReadOnlyList<long> EnabledIds(Run run) =>
        run.StartBuffsChosen || run.StartBuffIds.Count > 0 ? run.StartBuffIds : UnstoredFallback;

    private static long Sum(Run run, string type) => MdStartBuffs.Sum(EnabledIds(run), type);

    private static MdStartBuffEffect? Effect(Run run, string type) =>
        MdStartBuffs.EffectsOf(EnabledIds(run), type).FirstOrDefault();

    public static MdLevelOffsets LevelOffsets(Run run) => MdStartBuffs.LevelOffsets(EnabledIds(run));

    // ---- theme pack selection ----

    // "3 (up to 5 with Starter Buffs) Theme Packs are presented ... at the start of each Floor"
    // (wiki, Mirror of Names and Spiders): themePoolNum plus every ADDITIONAL_FLOOR_THEME_SLOT
    // (Star of the Beginning, Interstellar Travel, Perfected Possibility), capped at 5.
    public const long MaxThemePoolSize = 5;

    public static int ThemePoolSize(Run run) =>
        (int)Math.Min(MaxThemePoolSize, MdCommonTable.ThemePoolNum + Sum(run, StartBuffEffect.ThemeSlot));

    // Theme pack re-rolls per floor: themePoolRecreateCount (1) + Interstellar Travel's 2/3/4.
    // Both captured runs (102 only) re-roll at most 3 times on a floor.
    public static long ThemeRefreshAllowance(Run run) =>
        MdCommonTable.ThemePoolRecreateCount + Sum(run, StartBuffEffect.ThemeRefresh);

    // ---- shop ----

    // Shop E.G.O Gift Choice +1 (Star of the Beginning, Binary Star-shop), regular and super shops.
    public static long ShopGiftSlotBonus(Run run) => Sum(run, StartBuffEffect.ShopGiftSlot);

    // Free refreshes per shop visit: Star of the Beginning's normal ones (1/1/2), Moon Star-shop's
    // keyword ones (1/1/2). shop.fre / shop.fkre hold how many are LEFT this visit: set on entry,
    // spent by RefreshShop. (Counting the used ones, like rc, left the client showing no free
    // refresh - it reads the field as what is still available.)
    public static long FreeRefreshes(Run run) => Sum(run, StartBuffEffect.FreeRefresh);
    public static long FreeKeywordRefreshes(Run run) => Sum(run, StartBuffEffect.FreeKeywordRefresh);

    // Binary Star-shop's "greater chance for high Tier E.G.O Gifts to appear in Shops". The data
    // gives no number (value 1 on every level); Tier 3+ shop shares are doubled.
    public const double HighTierShopMultiplier = 1.5;

    public static double ShopHighTierMultiplier(Run run) =>
        Sum(run, StartBuffEffect.ShopHighTierChance) > 0 ? HighTierShopMultiplier : 1.0;

    // Moon Star-shop+/++: "+10% / +25% chance for Super Shop to appear", on top of the base odds.
    public static long SuperShopBonusPercent(Run run) => Sum(run, StartBuffEffect.EnhancedShopProb);

    // Cumulating Starcloud: "Increase E.G.O Gift sale price by 50/75/100% (once per Shop)". The
    // first sale of a visit gets the bonus and sets shop.aesp; later sales sell at the base price.
    public static long SellPrice(Run run, long basePrice)
    {
        var rate = Sum(run, StartBuffEffect.SellPriceRateOnce);
        if (rate <= 0 || run.Shop.Aesp != 0) return basePrice;
        run.Shop.Aesp = 1;
        return basePrice * (100 + rate) / 100;
    }

    // Shop entry (the first step onto a shop node): Binary Star-shop+'s owned-Tier sum as Cost,
    // then Chance Comet's Vestige. Returns the gifts granted.
    public static List<long> OnEnterShop(Run run)
    {
        var granted = new List<long>();

        // "Upon entering a Shop, add up all the Tier numbers of currently owned E.G.O Gifts and
        // gain it as Cost (max 75 / 150)". Gifts without a tier (hidden Adversity/enemy gifts)
        // add nothing; a Vestige counts as its tier.
        if (Effect(run, StartBuffEffect.TierTotalCostOnShop) is { } tierCost)
        {
            var total = run.Gifts.Items.Sum(g => OpenLethe.Server.MdEgoData.DetermineEgoTier(g.Id) ?? 0) * Math.Max(1, tierCost.value);
            run.Cost += Math.Min(total, tierCost.value2);
        }

        // "Upon entering a Shop, gain 1 'For Fuse or Sale only E.G.O Gift'" - a Vestige
        // (pieceEgoGiftIds 9991-9995, Tier 1-5). The value picks the tier band:
        //   1 (Chance Comet)   Tier 1~3
        //   2 (Chance Comet+)  Floor 5 or lower: Tier 1~3, Floor 6 or higher: Tier 2~3
        //   3 (Chance Comet++) Floor 5 or lower: Tier 2~3, Floor 6 or higher: Tier 3~4
        if (Effect(run, StartBuffEffect.VestigeOnEnterShop) is { } vestige)
        {
            var upperFloors = run.Floor.Current.F >= 5; // 0-based: index 5 is Floor 6
            var (lo, hi) = vestige.value switch
            {
                >= 3 => upperFloors ? (3, 4) : (2, 3),
                2 => upperFloors ? (2, 3) : (1, 3),
                _ => (1, 3),
            };
            var id = VestigeOfTier(Random.Shared.Next(lo, hi + 1));
            run.Gifts.Items.Add(new EgoGift { Id = id });
            granted.Add(id);
        }

        return granted;
    }

    // ---- cost ----

    // Binary Star-shop: "Encounter Reward Cost +20/30/40%", added to the other battle-cost rates.
    public static long WinBattleCostRate(Run run) => Sum(run, StartBuffEffect.WinBattleCostRate);

    // Star-shower+/++: "When Choosing Starlight from Encounter Reward Cards, Cost +75 / +150".
    public static long StarlightCardCost(Run run) => Sum(run, StartBuffEffect.StarlightCardCost);

    // Cumulating Starcloud: "When moving to the next floor, increase current Cost balance by
    // 10/20/30% (max 100/125/150)". Called when the next floor is generated.
    public static void OnMoveToNextFloor(Run run)
    {
        if (Effect(run, StartBuffEffect.CostRateOnClearFloor) is not { } rate || run.Cost <= 0) return;
        run.Cost += Math.Min(run.Cost * rate.value / 100, rate.value2);
    }

    // ---- gifts on entering floors ----

    // The buffs' floor gifts reach the player as a GetEgogift popup, the one the client shows with
    // gifts to click and take (AcquireReward grants what was picked, `se` of them): a gift granted
    // straight into egs shows up in the inventory with no popup at all.
    private static void QueueGiftPopup(Run run, List<long> pool, long picks)
    {
        if (pool.Count == 0 || picks <= 0) return;
        run.RewardEvents.Add(new RewardEvent
        {
            Rt = "GetEgogift",
            Se = Math.Min(picks, pool.Count),
            Sh = pool.Count,
            Pool = pool,
            PoolV2 = new(),
            PoolV3 = new(),
        });
    }

    // "Upon entry", with the starting gifts (AcquireStart) - Perfected Possibility:
    //   Dark Vestige / + Faint Vestige / + Twinkling Vestige, and
    //   1/2/3 Tier 3 gift(s) of the selected Keyword "of your choice": ONE popup offering every
    //   Tier 3 gift of that Keyword the run lacks, from which the player takes 1/2/3. Only when
    //   fewer Tier 3 gifts are left than picks do lower-Tier ones fill the gap ("if no remaining
    //   Gifts meet the conditions, a lower Tier one will appear instead").
    public static void OnEnterFirstFloor(Run run)
    {
        foreach (var vestige in FloorOneVestiges(run))
            run.Gifts.Items.Add(new EgoGift { Id = vestige });

        var picks = Sum(run, StartBuffEffect.ChosenTier3KeywordGifts);
        if (picks > 0)
        {
            var offer = Math.Max((int)picks, KeywordCandidates(run, 3).Count);
            QueueGiftPopup(run, KeywordGifts(run, 3, offer), picks);
        }
    }

    private static IEnumerable<long> FloorOneVestiges(Run run)
    {
        var effective = MdStartBuffs.Effective(EnabledIds(run)).SelectMany(b => b.effects).Select(e => e.type).ToHashSet();
        if (effective.Contains(StartBuffEffect.Vestige123OnFloor1)) return new[] { VestigeOfTier(1), VestigeOfTier(2), VestigeOfTier(3) };
        if (effective.Contains(StartBuffEffect.Vestige12OnFloor1)) return new[] { VestigeOfTier(1), VestigeOfTier(2) };
        if (effective.Contains(StartBuffEffect.Vestige1OnFloor1)) return new[] { VestigeOfTier(1) };
        return Array.Empty<long>();
    }

    // Floor gifts, queued when floor index `floor` (0-based) is generated:
    //   Moon Star-shop      Floor 1: 1/2/3 random Tier 1 gift(s)
    //   Chance Comet        Floor 3: 1 random Tier 3 gift
    //   Chance Comet+       Floor 3 / 5: 1 Tier 3 gift of the selected Keyword
    //   Chance Comet++      Floor 3 / 5 / 7: 1 Tier 4 gift of the selected Keyword
    // A floor's gifts share one popup in which every offered gift is taken. Returns the offer.
    public static List<long> OnEnterFloor(Run run, long floor)
    {
        var offered = new List<long>();
        var floorNumber = floor + 1;
        void Offer(long? id)
        {
            if (id is { } gift && !offered.Contains(gift)) offered.Add(gift);
        }

        if (floorNumber == 1)
            for (var i = 0; i < Sum(run, StartBuffEffect.Tier1GiftsOnFloor1); i++)
                Offer(RandomGift(run, tier: 1, keyword: null, exclude: offered));
        if (floorNumber == 3)
            for (var i = 0; i < Sum(run, StartBuffEffect.Tier3GiftOnFloor3); i++)
                Offer(RandomGift(run, tier: 3, keyword: null, exclude: offered));
        if (floorNumber is 3 or 5)
            for (var i = 0; i < Sum(run, StartBuffEffect.Tier3KeywordGiftOnFloor3And5); i++)
                Offer(KeywordGifts(run, 3, 1, offered).Cast<long?>().FirstOrDefault());
        if (floorNumber is 3 or 5 or 7)
            for (var i = 0; i < Sum(run, StartBuffEffect.Tier4KeywordGiftOnFloor357); i++)
                Offer(KeywordGifts(run, 4, 1, offered).Cast<long?>().FirstOrDefault());

        QueueGiftPopup(run, offered, offered.Count);
        return offered;
    }

    // ---- fusion ----

    // Chance Comet: "When Fusing, gain a random additional E.G.O Gift 2 Tiers (++: 1 Tier) below
    // the Fusion result (must be Tier 3 or higher) (once per Shop)". shop.aec marks it used for
    // the visit. Returns the bonus gift, or null.
    public static EgoGift? OnCombine(Run run, long resultGiftId)
    {
        if (Effect(run, StartBuffEffect.LowerTierGiftOnCombine) is not { } effect || run.Shop.Aec != 0) return null;
        var resultTier = OpenLethe.Server.MdEgoData.DetermineEgoTier(resultGiftId) ?? 0;
        if (resultTier < 3) return null;

        var bonusTier = Math.Max(1, resultTier - effect.value);
        if (RandomGift(run, bonusTier, keyword: null) is not { } id) return null;
        run.Shop.Aec = 1;
        var bonus = new EgoGift { Id = id };
        run.Gifts.Items.Add(bonus);
        return bonus;
    }

    // ---- gift pickers ----

    // mirror-dungeon-common-data pieceEgoGiftIds: Dark / Faint / Twinkling / Brilliant / Lunar Vestige.
    public static long VestigeOfTier(long tier) => 9990 + Math.Clamp(tier, 1, 5);

    // A random gift of `tier` from the season's general pool that the run does not own (and that
    // is not in `exclude`), of `keyword` when given.
    private static long? RandomGift(Run run, long tier, string? keyword, IReadOnlyCollection<long>? exclude = null)
    {
        var owned = SharedRules.OwnedGiftIds(run);
        var candidates = new MdThemePool().GeneralGiftPool()
            .Where(id => !owned.Contains(id) && (exclude is null || !exclude.Contains(id)))
            .Where(id => OpenLethe.Server.MdEgoData.DetermineEgoTier(id) == tier)
            .Where(id => keyword is null || OpenLethe.Server.MdEgoData.GetById(id)?.keyword == keyword)
            .ToList();
        return candidates.Count == 0 ? null : candidates[Random.Shared.Next(candidates.Count)];
    }

    // The not-owned gifts of `tier` in the run's selected Keyword category - the starting gift
    // set's keyword (run.StartKeyword); a run with no keyword gets every keyword.
    private static List<long> KeywordCandidates(Run run, long tier, IReadOnlyCollection<long>? exclude = null)
    {
        var keyword = string.IsNullOrEmpty(run.StartKeyword) || run.StartKeyword == "None" ? null : run.StartKeyword;
        var owned = SharedRules.OwnedGiftIds(run);
        return new MdThemePool().GeneralGiftPool()
            .Where(id => !owned.Contains(id) && (exclude is null || !exclude.Contains(id)))
            .Where(id => OpenLethe.Server.MdEgoData.DetermineEgoTier(id) == tier)
            .Where(id => keyword is null || OpenLethe.Server.MdEgoData.GetById(id)?.keyword == keyword)
            .ToList();
    }

    // Up to `count` distinct Keyword gifts, `tier` first; lower Tiers only fill what `tier` cannot.
    private static List<long> KeywordGifts(Run run, long tier, int count, IReadOnlyCollection<long>? exclude = null)
    {
        var result = new List<long>();
        for (var t = tier; t >= 1 && result.Count < count; t--)
        {
            var taken = exclude is null ? result : result.Concat(exclude).ToList();
            result.AddRange(OpenLethe.Server.MdMapGen.ChooseMultiple(KeywordCandidates(run, t, taken), count - result.Count));
        }
        return result;
    }
}
