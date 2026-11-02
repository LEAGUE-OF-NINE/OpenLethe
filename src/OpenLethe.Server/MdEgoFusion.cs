using OpenLethe.Server.MirrorDungeon.Data;

namespace OpenLethe.Server;

// Port of lethe-server models/src/mirror_dungeon/ego_gift_fusion.rs.

/// static-data/mirror-dungeon-common-data/*.json — each file is ONE object (unlisted).
public sealed class EgoGiftCombineFixedTable
{
    public CombineFixed? egoGiftCombineFixedTable;
}

public sealed class CombineFixed
{
    public List<FixedCombination> combineFixed = new();
    // Mixed recipe: N gifts from group A plus M from group B (e.g. 2 A + 3 B => 9083).
    public List<MixedCombination> combineMixed = new();
}

public sealed class MixedCombination
{
    public List<long> aEgoGiftIds = new();
    public long aEgoGiftRequiredNum;
    public List<long> bEgoGiftIds = new();
    public long bEgoGiftRequiredNum;
    public long resultEgoGiftId;
}

public sealed class FixedCombination
{
    public long? aEgoGiftId;
    public long? bEgoGiftId;
    public long? cEgoGiftId;
    public long resultEgoGiftId;
    public List<long> requiredEgoGiftIds = new();
}

/// static-data/mirror-dungeon-common-data/*.json - the same unlisted files the fixed-combination
/// table comes from. Rust: models/src/mirror_dungeon/ego_gift_fusion.rs EgoGiftCombineTierTable.
public sealed class EgoGiftCombineTierTable
{
    public CombineTierData? egoGiftCombineTierTable;
}

public sealed class CombineTierData
{
    // Story-MD's tier-by-tier recipes.
    public List<CombineTwo> combineTwo = new();
    public List<CombineThree> combineThree = new();
    // The plain fusion's score per material tier and the result tier per score range (the
    // enhanced table is a super shop's).
    public List<CombineScoreByTier> combineScoreByEgoGiftTier = new();
    public List<CombineTierResult> combineTierResultByCombineScore = new();
    public List<CombineTierResult> enhancedCombineTierResultByCombineScore = new();
}

public sealed class CombineScoreByTier
{
    public long egoGiftTier;
    public long combineScore;
}

public sealed class CombineTierResult
{
    public long combineScoreRangeMin;
    public long combineScoreRangeMax;
    public long tierResult;
}

public sealed class CombineTwo
{
    public long aTier;
    public long bTier;
    public long resultTier;
}

public sealed class CombineThree
{
    public long aTier;
    public long bTier;
    public long cTier;
    public long resultTier;
}

/// Fixed-recipe result when FixedGiftId is non-null; otherwise a random (Keyword, Tier) roll.
/// ponytail: one record struct instead of a two-case class hierarchy - the caller only
/// ever branches on "was it fixed".
public readonly record struct MdFuseResult(long? FixedGiftId, string? Keyword, long Tier);

public static class MdEgoFusion
{
    // Every table here is mirror-dungeon-common-data's, with the request's upload laid over it
    // (MdCommonSource.Current); the derived maps are memoized per source, so an upload's recipes
    // never leak into another account's fusions.
    private static CombineFixed? FixedTable(MdCommonSource source) =>
        source.View<EgoGiftCombineFixedTable>().egoGiftCombineFixedTable;

    private static List<FixedCombination> FixedCombinations => FixedTable(MdCommonSource.Current)?.combineFixed ?? new();

    private static List<MixedCombination> MixedCombinations => FixedTable(MdCommonSource.Current)?.combineMixed ?? new();

    private static Dictionary<long, long> RecipeMapping => MdCommonSource.Current.Derived("fusion.recipeMapping", source =>
    {
        var map = new Dictionary<long, long>();
        foreach (var c in FixedTable(source)?.combineFixed ?? new())
        {
            // Rust collects into a HashMap: duplicate hashes are last-wins, so assign
            // through the indexer rather than ToDictionary (which would throw).
            map[Hash3(c.aEgoGiftId ?? 0, c.bEgoGiftId ?? 0, c.cEgoGiftId ?? 0)] = c.resultEgoGiftId;
        }
        return map;
    });

    public static IReadOnlyDictionary<long, long> EgoRecipeMapping => RecipeMapping;

    // Fixed-recipe result -> every gift it is made from: each of its recipe rows' a/b/c and
    // requiredEgoGiftIds, and those components' own components (9176 Impending Wave -> 9049,
    // 9125, 9175 Surging Globe -> 9172, 9173). A run holding the result counts these as owned.
    // The mixed recipe (9083 Lunar Memory) is deliberately not in here: its parts stay obtainable.
    private static Dictionary<long, HashSet<long>> ComponentsByResult => MdCommonSource.Current.Derived("fusion.components", source =>
    {
        var direct = new Dictionary<long, HashSet<long>>();
        foreach (var c in FixedTable(source)?.combineFixed ?? new())
        {
            if (!direct.TryGetValue(c.resultEgoGiftId, out var parts))
                direct[c.resultEgoGiftId] = parts = new HashSet<long>();
            foreach (var id in c.requiredEgoGiftIds.Concat(new[] { c.aEgoGiftId, c.bEgoGiftId, c.cEgoGiftId }
                         .Where(id => id is not null).Select(id => id!.Value)))
                if (id != 0 && id != c.resultEgoGiftId) parts.Add(id);
        }

        var closed = new Dictionary<long, HashSet<long>>();
        foreach (var result in direct.Keys)
        {
            var all = new HashSet<long>();
            var stack = new Stack<long>(direct[result]);
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                if (id == result || !all.Add(id)) continue;
                if (direct.TryGetValue(id, out var sub))
                    foreach (var s in sub) stack.Push(s);
            }
            closed[result] = all;
        }
        return closed;
    });

    public static IReadOnlyCollection<long> FusionComponents(long resultEgoGiftId) =>
        ComponentsByResult.TryGetValue(resultEgoGiftId, out var parts) ? parts : Array.Empty<long>();

    // Rust get_md_ego_gift_combinations() takes the combine table of the common data; the file
    // carries many other top-level keys, which STJ ignores.
    public static CombineTierData? CombineTierTable =>
        MdCommonSource.Current.View<EgoGiftCombineTierTable>().egoGiftCombineTierTable;

    /// Port of combine_ego_gift_story_mirror_dungeon.rs tier_to_int. Deliberately NOT
    /// MdEgoData.DetermineEgoTier: this reads tag.First() only and collapses TIER_4, TIER_5
    /// and a missing/unknown tag all into 4, where DetermineEgoTier scans for any TIER_ tag
    /// and returns 5 or null respectively. Story-MD's combine uses this one.
    public static long TierToInt(long egoGiftId) =>
        MdEgoData.GetById(egoGiftId)?.tag.FirstOrDefault() switch
        {
            "TIER_1" => 1,
            "TIER_2" => 2,
            "TIER_3" => 3,
            _ => 4,
        };

    /// Rust hash_3_numbers: all arithmetic wraps (wrapping_add/mul on i64).
    public static long Hash3(long a, long b, long c)
    {
        unchecked
        {
            var sum = a + b + c;
            var prod = (a + 1) * (b + 1) * (c + 1); // +1 to avoid 0
            return sum * sum + prod;
        }
    }

    public static long? FuseGiftFixed(IReadOnlyList<long> inputGifts)
    {
        // Match the retail requiredEgoGiftIds as an exact multiset first (covers 2-, 3- and
        // 4-gift recipes), then the mixed A+B recipes, and only then the old a/b/c hash.
        var ids = inputGifts.Where(i => i != 0).OrderBy(i => i).ToList();
        if (ids.Count > 0)
        {
            foreach (var c in FixedCombinations)
            {
                var req = (c.requiredEgoGiftIds ?? new List<long>()).Where(i => i != 0).OrderBy(i => i).ToList();
                if (req.Count == 0 || req.Count != ids.Count) continue;
                var same = true;
                for (var i = 0; i < req.Count; i++) if (req[i] != ids[i]) { same = false; break; }
                if (same) return c.resultEgoGiftId;
            }

            foreach (var m in MixedCombinations)
            {
                var needA = m.aEgoGiftRequiredNum; var needB = m.bEgoGiftRequiredNum;
                if (ids.Count != needA + needB) continue;
                var setA = new HashSet<long>(m.aEgoGiftIds); var setB = new HashSet<long>(m.bEgoGiftIds);
                var cntA = ids.Count(i => setA.Contains(i)); var cntB = ids.Count(i => setB.Contains(i));
                if (cntA == needA && cntB == needB) return m.resultEgoGiftId;
            }
        }

        var hash = Hash3(
            inputGifts.Count > 0 ? inputGifts[0] : 0,
            inputGifts.Count > 1 ? inputGifts[1] : 0,
            inputGifts.Count > 2 ? inputGifts[2] : 0);
        return EgoRecipeMapping.TryGetValue(hash, out var result) ? result : null;
    }

    // combineScoreByEgoGiftTier; the switch is the shipped table, for a tier it does not list.
    public static long DetermineEgoCombineScore(long egoGiftId)
    {
        var tier = MdEgoData.DetermineEgoTier(egoGiftId);
        if (tier is { } t && CombineTierTable?.combineScoreByEgoGiftTier.FirstOrDefault(r => r.egoGiftTier == t) is { } row)
            return row.combineScore;
        return tier switch
        {
            1 => 3,
            2 => 6,
            3 => 10,
            4 => 15,
            5 => 30,
            _ => 0,
        };
    }

    // (enhanced)CombineTierResultByCombineScore: the row whose range starts at or below the score,
    // the lowest row below every range (the shipped tables start at 6, but two Tier 1s score 6 and
    // a lone material less). The switches are the shipped tables, for an empty one.
    public static long DetermineOutputEgoTier(long combineScore, bool isSuperShop)
    {
        var table = CombineTierTable;
        var rows = (isSuperShop ? table?.enhancedCombineTierResultByCombineScore : table?.combineTierResultByCombineScore)?
            .OrderBy(r => r.combineScoreRangeMin).ToList();
        if (rows is { Count: > 0 })
            return (rows.LastOrDefault(r => r.combineScoreRangeMin <= combineScore) ?? rows[0]).tierResult;

        return isSuperShop
            ? combineScore switch
            {
                >= 0 and <= 9 => 1,
                >= 10 and <= 14 => 2,
                >= 15 and <= 21 => 3,
                _ => 4,
            }
            : combineScore switch
            {
                >= 0 and <= 10 => 1,
                >= 11 and <= 16 => 2,
                >= 17 and <= 24 => 3,
                _ => 4,
            };
    }

    // egogiftRandomCombineProbs.origin: the row for this many materials, else the nearest one
    // below (or the smallest). The fallbacks are the shipped table, for an empty one.
    public static float DetermineMatchKeywordProb(int numInputGifts)
    {
        var probs = MdCommonTable.RandomCombineProbs;
        if (probs.Count > 0)
            return (float)(probs.LastOrDefault(p => p.materialCount <= numInputGifts) ?? probs[0]).prob;

        if (numInputGifts <= 2) return 0.6f;
        if (numInputGifts == 3) return 0.9f;
        return 0.99f;
    }

    public static MdFuseResult FuseGift(string keyword, bool isSuperShop, IReadOnlyList<long> inputGifts)
    {
        if (FuseGiftFixed(inputGifts) is { } fixedGift)
            return new MdFuseResult(fixedGift, null, 0);

        var combineScore = inputGifts.Sum(DetermineEgoCombineScore);
        var outputTier = DetermineOutputEgoTier(combineScore, isSuperShop);
        var shouldMatchKeyword = Random.Shared.NextSingle() < DetermineMatchKeywordProb(inputGifts.Count);
        return new MdFuseResult(null, shouldMatchKeyword ? keyword : null, outputTier);
    }
}
