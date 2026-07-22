using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenLethe.Server;

// Port of lethe-server server/src/api/md/map/themepoolselector.rs ThemePool.
public sealed class MdThemePool
{
    public readonly Dictionary<long, ThemeStatic> pools;

    // ponytail: no DB custom-theme table in this port; vanilla loads static themes only.
    public MdThemePool()
    {
        pools = MdThemeData.AllThemes().ToDictionary(t => t.id, t => t);
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

    public static List<long> EgoGiftWeighted(long floor, long egoId)
    {
        var tier = (MdEgoData.DetermineEgoTier(egoId) ?? 1) - 1;
        var weights = GetEgoGiftWeights(floor);
        var prob = tier >= 0 && tier < weights.Length ? weights[tier] : 0;
        var weight = (int)(100 * prob);
        return Enumerable.Repeat(egoId, weight).ToList();
    }

    public static long? SelectRandomEgoFromPool(List<long> pool, long floor, Func<long, bool> filter)
    {
        var weighted = pool.Where(filter).SelectMany(id => EgoGiftWeighted(floor, id)).ToList();
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

    public List<long> ShopEgoGiftsPool(long themeId)
    {
        var dropPools = MdThemeData.DropPools();
        if (dropPools.Count == 0) return new List<long>();
        var maxDungeonId = dropPools.Max(p => p.dungeonId);
        var dropPool = dropPools.FirstOrDefault(p => p.dungeonId == maxDungeonId);
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

        // Rust removes every fixed-recipe OUTPUT from the shop pool (those are craft-only).
        giftPool.ExceptWith(MdEgoFusion.EgoRecipeMapping.Values);

        return giftPool.ToList();
    }

    public List<long> SelectRandomShopEgos(long themeId, int count, long floor, string? keyword)
    {
        var giftPool = ShopEgoGiftsPool(themeId);
        var chosen = new List<long>();

        var matchingCount = (count + 1) / 2; // div_ceil(2)
        for (var i = 0; i < matchingCount; i++)
        {
            var picked = SelectRandomEgoFromPool(giftPool, floor, id =>
                !chosen.Contains(id) && MdEgoData.GetById(id) is { } g && g.keyword == keyword);
            if (picked is { } id2) chosen.Add(id2);
        }

        var remaining = count - chosen.Count;
        for (var i = 0; i < remaining; i++)
        {
            var picked = SelectRandomEgoFromPool(giftPool, floor, id =>
                !chosen.Contains(id) && MdEgoData.GetById(id) is { } g && g.keyword != keyword);
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
