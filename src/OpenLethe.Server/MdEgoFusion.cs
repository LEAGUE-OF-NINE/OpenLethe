using System;
using System.Collections.Generic;
using System.Linq;
using OpenLethe.Resources;

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
    public List<CombineTwo> combineTwo = new();
    public List<CombineThree> combineThree = new();
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
    private static readonly Lazy<List<FixedCombination>> FixedCombinations = new(() =>
        StaticData.GetUnlisted<EgoGiftCombineFixedTable>("static-data/mirror-dungeon-common-data")
            .Where(t => t.egoGiftCombineFixedTable is not null)
            .SelectMany(t => t.egoGiftCombineFixedTable!.combineFixed)
            .ToList());

    private static readonly Lazy<Dictionary<long, long>> RecipeMapping = new(() =>
    {
        var map = new Dictionary<long, long>();
        foreach (var c in FixedCombinations.Value)
        {
            // Rust collects into a HashMap: duplicate hashes are last-wins, so assign
            // through the indexer rather than ToDictionary (which would throw).
            map[Hash3(c.aEgoGiftId ?? 0, c.bEgoGiftId ?? 0, c.cEgoGiftId ?? 0)] = c.resultEgoGiftId;
        }
        return map;
    });

    public static IReadOnlyDictionary<long, long> EgoRecipeMapping => RecipeMapping.Value;

    // Rust get_md_ego_gift_combinations() loads the folder unlisted and the combine handler
    // takes .first(); the file carries many other top-level keys, which STJ ignores.
    private static readonly Lazy<CombineTierData?> TierTable = new(() =>
        StaticData.GetUnlisted<EgoGiftCombineTierTable>("static-data/mirror-dungeon-common-data")
            .Select(t => t.egoGiftCombineTierTable)
            .FirstOrDefault(t => t is not null));

    public static CombineTierData? CombineTierTable => TierTable.Value;

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
        var hash = Hash3(
            inputGifts.Count > 0 ? inputGifts[0] : 0,
            inputGifts.Count > 1 ? inputGifts[1] : 0,
            inputGifts.Count > 2 ? inputGifts[2] : 0);
        return EgoRecipeMapping.TryGetValue(hash, out var result) ? result : null;
    }

    public static long DetermineEgoCombineScore(long egoGiftId) =>
        MdEgoData.DetermineEgoTier(egoGiftId) switch
        {
            1 => 3,
            2 => 6,
            3 => 10,
            4 => 15,
            5 => 30,
            _ => 0,
        };

    public static long DetermineOutputEgoTier(long combineScore, bool isSuperShop) => isSuperShop
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

    public static float DetermineMatchKeywordProb(int numInputGifts)
    {
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
