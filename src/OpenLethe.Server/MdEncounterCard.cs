using System;
using System.Collections.Generic;
using System.Linq;
using OpenLethe.Resources;

namespace OpenLethe.Server;

// Port of lethe-server models/src/mirror_dungeon/encounter_card.rs and
// models/src/data/encounter_card.rs.

public sealed class MdRewardList
{
    public long groupID;
    public List<MdReward> rewardCaseList = new();
}

/// ponytail: Rust models this as an internally-tagged enum on rewardType. System.Text.Json
/// polymorphism needs the discriminator, and these files put "id" first - so this is one
/// flat type with every variant's params nullable, dispatched on the rewardType string.
/// Consequence: the flat model is tolerant where Rust's enum is strict. Rust's Reward enum
/// has exactly 4 variants (COST, EGOGIFT, EGOSTOCK, COST_EGOGIFT_START_CATEGORY), so
/// group-D's 7 STARLIGHT_MIN_MAX cards (ids 401-407) fail to deserialize and Rust drops
/// that whole file - its ENCOUNTER_REWARD_MAP has 28 entries to this type's 35. Those 7
/// extras are unreachable here too: STARLIGHT_MIN_MAX is in neither AllowedCardTypes nor
/// any switch arm below, so no response ever differs. Do not add a filter for them.
/// The same tolerant-vs-strict divergence applies to a missing rewardParams: Rust's
/// per-variant reward_params field is non-Option, so a card missing it fails
/// deserialization and drops the whole file, whereas here it just leaves rewardParams
/// null and the battle-reward handler silently skips that one card via `if (rp is null)
/// continue`.
public sealed class MdReward
{
    public long id;
    public string rewardType = "";
    public long rewardLV;
    public string localizeTextFormat = "";
    public MdRewardParams? rewardParams;
}

public sealed class MdRewardParams
{
    public long? acquireCostMin;
    public long? acquireCostMax;
    public double? egoGiftAcquirableProb;
    public MdTierRange? egoGiftTierRange;
    public MdEgoStock? leastEgoStock;
    public MdEgoStock? randomEgoStock;
}

public sealed class MdTierRange
{
    public long min;
    public long max;

    public bool WithinRange(long value) => value >= min && value <= max;
}

public sealed class MdEgoStock
{
    public long kind;
    public long num;
}

public static class MdEncounterCard
{
    private static readonly Lazy<List<MdRewardList>> Groups = new(() =>
        StaticData.GetList<MdRewardList>("static-data/mirrordungeon-battle-reward-case-group"));

    public static readonly IReadOnlySet<string> AllowedCardTypes = new HashSet<string>
    {
        "dungeon_battle_reward_case_format_random_gift_acquisition",
        "dungeon_battle_reward_case_format_cost_acquisition",
        "dungeon_battle_reward_case_format_cost_and_gift_acquisition_from_start_keyword",
        "dungeon_battle_reward_case_format_least_ego_resource_acquisition",
    };

    private static readonly Lazy<Dictionary<long, MdReward>> RewardMap = new(() =>
    {
        var map = new Dictionary<long, MdReward>();
        foreach (var group in Groups.Value)
            foreach (var reward in group.rewardCaseList)
                map[reward.id] = reward; // last wins, as Rust HashMap::insert does
        return map;
    });

    public static IReadOnlyDictionary<long, MdReward> EncounterRewardMap => RewardMap.Value;

    /// One random card per allowed localizeTextFormat group, restricted to rewardLV <= maxLevel.
    public static List<long> PickRandomEncounterCards(long maxLevel)
    {
        var groups = new Dictionary<string, List<long>>();
        foreach (var group in Groups.Value)
        {
            foreach (var reward in group.rewardCaseList)
            {
                if (!AllowedCardTypes.Contains(reward.localizeTextFormat)) continue;
                if (reward.rewardLV > maxLevel) continue;
                if (!groups.TryGetValue(reward.localizeTextFormat, out var list))
                    groups[reward.localizeTextFormat] = list = new List<long>();
                list.Add(reward.id);
            }
        }

        // ponytail: Rust iterates a HashMap (random order); Dictionary is insertion-ordered.
        // The caller samples from this list anyway, so the order difference is not observable.
        return groups.Values.Select(v => v[Random.Shared.Next(v.Count)]).ToList();
    }
}
