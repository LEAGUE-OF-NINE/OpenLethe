namespace OpenLethe.Server.MirrorDungeon.Data;

// Port of lethe-server server/src/api/md/map/themepoolselector.rs ThemePool.
public sealed class MdThemePool
{
    public readonly Dictionary<long, ThemeStatic> pools;

    // The static themes plus the account's uploaded packs (Account.CustomTheme, via
    // MdCatalog.Current - Rust's ThemePool::load_from_db_or_default), so a custom pack's floor
    // finds its pack wherever a rule builds a pool.
    public MdThemePool()
    {
        pools = MdThemeData.AllThemes().ToDictionary(t => t.id, t => t);
    }

    // Further packs laid over those (an id given here wins). The rules' customThemes parameters
    // feed this; handlers no longer pass them, since MdCatalog already carries the account's.
    public MdThemePool(IEnumerable<ThemeStatic>? customThemes) : this()
    {
        foreach (var t in customThemes ?? Enumerable.Empty<ThemeStatic>())
            if (t is not null && t.id != 0) pools[t.id] = t;
    }

    public static List<long> GetCombinedEgoGiftPool(ThemeStatic t)
    {
        var combined = new List<long>(t.egoGiftPool);
        combined.AddRange(t.specificEgoGiftPool);
        return combined;
    }

    public static double[] GetEgoGiftWeights(long floor) => floor switch
    {
        0 => new[] { .75, .25 },
        1 => new[] { .625, .25, .125 },
        _ => new[] { .42, .26, .18, .13, .01 },
    };

    // weightMultiplier is the shop-keyword boost (shopKeywordRefreshWeightMultiplier, 12.5): an
    // on-keyword gift weighs that much more than its tier share alone would give it.
    // highTierMultiplier scales the Tier 3+ shares (Binary Star-shop's high-tier shop chance).
    public static List<long> EgoGiftWeighted(long floor, long egoId, double weightMultiplier = 1.0,
        double highTierMultiplier = 1.0)
    {
        var tier = (MdEgoData.DetermineEgoTier(egoId) ?? 1) - 1;
        var weights = GetEgoGiftWeights(floor);
        var prob = tier >= 0 && tier < weights.Length ? weights[tier] : 0;
        if (tier >= 2) prob *= highTierMultiplier;
        var weight = (int)(100 * prob * weightMultiplier);
        return Enumerable.Repeat(egoId, weight).ToList();
    }

    public static long? SelectRandomEgoFromPool(List<long> pool, long floor, Func<long, bool> filter, double weightMultiplier = 1.0,
        double highTierMultiplier = 1.0)
    {
        var weighted = pool.Where(filter).SelectMany(id => EgoGiftWeighted(floor, id, weightMultiplier, highTierMultiplier)).ToList();
        return weighted.Count == 0 ? null : weighted[Random.Shared.Next(weighted.Count)];
    }

    public List<long> SelectRandomEgosFromPool(long themeId, int count, long floor)
    {
        if (!pools.TryGetValue(themeId, out var theme)) return new List<long>();
        var combined = GetCombinedEgoGiftPool(theme);

        var chosen = new List<long>();
        for (var i = 0; i < count; i++)
        {
            var picked = SelectRandomEgoFromPool(combined, floor, id => !chosen.Contains(id));
            if (picked is { } id2) chosen.Add(id2);
        }

        Shuffle(chosen);
        return chosen;
    }

    // The current season's drop pool (the newest dungeonId).
    private static DungeonEgoGiftDropPool? SeasonDropPool() =>
        MdThemeData.DropPools().OrderByDescending(p => p.dungeonId).FirstOrDefault();

    // The season's excludeEgoGifts when they apply to a run of this difficulty (see
    // DungeonEgoGiftDropPool.ExcludesFor), empty otherwise.
    private static HashSet<long> ExcludedGifts(DungeonEgoGiftDropPool? dropPool, long? dungeonIdx) =>
        dropPool is not null && dropPool.ExcludesFor(dungeonIdx)
            ? new HashSet<long>(dropPool.excludeEgoGifts)
            : new HashSet<long>();

    // What the pack itself hands out: its egoGiftPool plus its exclusive (specific) gifts, minus
    // the gifts the season's drop pool excludes - the hard-only ones (9156, 9160, ...) that
    // nearly every built-in pack lists but a normal run may not get. Empty for an unknown pack.
    // dungeonIdx: the run's difficulty (run.Idx); null keeps the normal-mode exclusion.
    public List<long> ThemeDropPool(long themeId, long? dungeonIdx = null)
    {
        if (!pools.TryGetValue(themeId, out var theme)) return new List<long>();
        var excluded = ExcludedGifts(SeasonDropPool(), dungeonIdx);
        return GetCombinedEgoGiftPool(theme).Where(id => !excluded.Contains(id)).Distinct().ToList();
    }

    // dungeonIdx: the run's difficulty (run.Idx). The season table only excludes its
    // excludeEgoGifts from normal runs (excludeTargetIdxList [0]), so a hard run's shop sells the
    // hard-only gifts too; null keeps the normal-mode pool.
    public List<long> ShopEgoGiftsPool(long themeId, long? dungeonIdx = null)
    {
        var dropPool = SeasonDropPool();
        if (dropPool is null) return new List<long>();

        var giftPool = new HashSet<long>(dropPool.egoGifts);
        giftPool.ExceptWith(ExcludedGifts(dropPool, dungeonIdx));

        // Rust removes every theme's specific_ego_gift_pool (including this one), then
        // re-adds this theme's - net effect: only this theme's specific pool survives.
        foreach (var t in pools.Values)
            giftPool.ExceptWith(t.specificEgoGiftPool);

        if (!pools.TryGetValue(themeId, out var theme)) return new List<long>();
        foreach (var id in theme.specificEgoGiftPool)
            giftPool.Add(id);

        // The pack's own gifts are always on sale on its floor. For every built-in pack this adds
        // nothing (its ThemeDropPool already sits inside the season pool plus its specific gifts);
        // a custom pack's egoGiftPool is how its gifts reach the shop at all.
        giftPool.UnionWith(ThemeDropPool(themeId, dungeonIdx));

        // Rust removes every fixed-recipe OUTPUT from the shop pool (those are craft-only).
        giftPool.ExceptWith(MdEgoFusion.EgoRecipeMapping.Values);

        // Never on sale in any mode: the globally excluded gifts - Wealth (9800), A Certain
        // Philosophy (9799), the NO_SALE ones, ... Wealth and 9799 sit in the season pool and
        // outside excludeEgoGifts, so only this keeps them off the shelves.
        giftPool.ExceptWith(dropPool.globalExcludeEgoGifts);

        return giftPool.ToList();
    }

    // The gifts a start buff hands out with no pack attached ("gain a random Tier 1 E.G.O Gift"):
    // the season's drop pool less its excluded and globally excluded gifts, every pack's exclusives
    // and the fusion-only recipe outputs. Vestiges (9991-9995) are not in it.
    public List<long> GeneralGiftPool()
    {
        var dropPool = SeasonDropPool();
        if (dropPool is null) return new List<long>();

        var giftPool = new HashSet<long>(dropPool.egoGifts);
        giftPool.ExceptWith(dropPool.excludeEgoGifts);
        giftPool.ExceptWith(dropPool.globalExcludeEgoGifts);
        foreach (var t in pools.Values)
            giftPool.ExceptWith(t.specificEgoGiftPool);
        giftPool.ExceptWith(MdEgoFusion.EgoRecipeMapping.Values);
        return giftPool.OrderBy(id => id).ToList();
    }

    // owned: gifts the run already has. A shop never offers those, so they leave the pool up front
    // (rather than being filtered out of the result) and every slot still fills with one the run lacks.
    // highTierMultiplier: see EgoGiftWeighted. dungeonIdx: the run's difficulty, see ShopEgoGiftsPool.
    public List<long> SelectRandomShopEgos(long themeId, int count, long floor, string? keyword,
        IReadOnlyCollection<long>? owned = null, double highTierMultiplier = 1.0, long? dungeonIdx = null)
    {
        var giftPool = ShopEgoGiftsPool(themeId, dungeonIdx);
        if (owned is { Count: > 0 }) giftPool.RemoveAll(owned.Contains);
        var chosen = new List<long>();

        // No real keyword (the shop's first roll sends null, a plain refresh the literal "None"):
        // every slot draws from the whole pool. Splitting it into "keyword == keyword" and the
        // rest filled half the shop from the few gifts whose own keyword is that very value - the
        // ten "None" economy gifts (Rebate Token, Pamphlet, Special Catalogue, ...) on a refresh,
        // the keyword-less ones on entry.
        if (string.IsNullOrEmpty(keyword) || keyword == "None")
        {
            for (var i = 0; i < count; i++)
            {
                var picked = SelectRandomEgoFromPool(giftPool, floor, id =>
                    !chosen.Contains(id) && MdEgoData.GetById(id) is not null, 1.0, highTierMultiplier);
                if (picked is { } id2) chosen.Add(id2);
            }

            Shuffle(chosen);
            return chosen;
        }

        // A real keyword weights on-keyword gifts by the retail table's
        // shopKeywordRefreshWeightMultiplier on top of their tier weight.
        var keywordBoost = MdCommonTable.KeywordRefreshWeightMultiplier;

        var matchingCount = (count + 1) / 2; // div_ceil(2)
        for (var i = 0; i < matchingCount; i++)
        {
            var picked = SelectRandomEgoFromPool(giftPool, floor, id =>
                !chosen.Contains(id) && MdEgoData.GetById(id) is { } g && g.keyword == keyword, keywordBoost, highTierMultiplier);
            if (picked is { } id2) chosen.Add(id2);
        }

        var remaining = count - chosen.Count;
        for (var i = 0; i < remaining; i++)
        {
            var picked = SelectRandomEgoFromPool(giftPool, floor, id =>
                !chosen.Contains(id) && MdEgoData.GetById(id) is { } g && g.keyword != keyword, 1.0, highTierMultiplier);
            if (picked is { } id2) chosen.Add(id2);
        }

        Shuffle(chosen);
        return chosen;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
