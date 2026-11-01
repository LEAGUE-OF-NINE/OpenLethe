namespace OpenLethe.Server.MirrorDungeon.Data;

// Port of lethe-server server/src/api/md/map/themepoolselector.rs ThemePool.
public sealed class MdThemePool
{
    public readonly Dictionary<long, ThemeStatic> pools;

    // Vanilla static themes only. POST /custom/upload/mirrordungeon-theme-floor fills
    // Account.CustomTheme; the overload below lays those packs over the static ones (Rust's
    // ThemePool::load_from_db_or_default). Every rule that reads the CURRENT floor's pack -
    // floor generation, starlight detection, the shop and its refresh, fusion, battle drops,
    // reward cards - takes the account's packs from its handler and must use that overload, or a
    // custom pack's floor finds no pack and every gift list comes back empty.
    public MdThemePool()
    {
        pools = MdThemeData.AllThemes().ToDictionary(t => t.id, t => t);
    }

    // The account's uploaded custom packs laid over the static ones (an uploaded id wins).
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

    // What the pack itself hands out: its egoGiftPool plus its exclusive (specific) gifts, minus
    // the gifts the season's drop pool excludes - the fusion-only ones (9156, 9160, ...) that
    // nearly every built-in pack lists but nothing may drop. Empty for an unknown pack.
    public List<long> ThemeDropPool(long themeId)
    {
        if (!pools.TryGetValue(themeId, out var theme)) return new List<long>();
        var excluded = new HashSet<long>(SeasonDropPool()?.excludeEgoGifts ?? new List<long>());
        return GetCombinedEgoGiftPool(theme).Where(id => !excluded.Contains(id)).Distinct().ToList();
    }

    public List<long> ShopEgoGiftsPool(long themeId)
    {
        var dropPool = SeasonDropPool();
        if (dropPool is null) return new List<long>();

        var giftPool = new HashSet<long>(dropPool.egoGifts);
        giftPool.ExceptWith(dropPool.excludeEgoGifts);

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
        giftPool.UnionWith(ThemeDropPool(themeId));

        // Rust removes every fixed-recipe OUTPUT from the shop pool (those are craft-only).
        giftPool.ExceptWith(MdEgoFusion.EgoRecipeMapping.Values);

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
    // highTierMultiplier: see EgoGiftWeighted.
    public List<long> SelectRandomShopEgos(long themeId, int count, long floor, string? keyword,
        IReadOnlyCollection<long>? owned = null, double highTierMultiplier = 1.0)
    {
        var giftPool = ShopEgoGiftsPool(themeId);
        if (owned is { Count: > 0 }) giftPool.RemoveAll(owned.Contains);
        var chosen = new List<long>();

        // A real keyword (anything but empty / the literal "None" a plain refresh sends) weights
        // on-keyword gifts by the retail table's shopKeywordRefreshWeightMultiplier on top of
        // their tier weight.
        var keywordBoost = string.IsNullOrEmpty(keyword) || keyword == "None"
            ? 1.0
            : MdCommonTable.KeywordRefreshWeightMultiplier;

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
