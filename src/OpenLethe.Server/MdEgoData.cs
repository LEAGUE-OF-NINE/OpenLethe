using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using OpenLethe.Resources;

namespace OpenLethe.Server;

// Port of lethe-server/models/src/data/mod.rs get_md_ego_by_id +
// models/src/mirror_dungeon/ego.rs Ego (only the fields MD-shop handlers need).
public sealed class MdEgo
{
    public long id;
    public long price;
    public List<JsonNode>? upgradeDataList;
    public List<string> tag = new();
    public string? keyword;
}

public static class MdEgoData
{
    // ponytail: memoize the folder scan - handlers call this per-request; data is static.
    private static readonly Lazy<List<MdEgo>> Data =
        new(() => StaticData.GetList<MdEgo>("static-data/ego-gift-mirrordungeon"));

    public static MdEgo? GetById(long id) => Data.Value.FirstOrDefault(e => e.id == id);

    public static List<long> AllIds() => Data.Value.Select(e => e.id).ToList();

    public static long UpgradeCost(long price, long desiredUl) => ((price * desiredUl / 3) / 10) * 10;

    // Port of models/src/mirror_dungeon/ego_gift_fusion.rs determine_ego_tier.
    public static long? DetermineEgoTier(long id)
    {
        var tag = GetById(id)?.tag.FirstOrDefault(t => t.StartsWith("TIER_", StringComparison.Ordinal));
        if (tag is null) return null;
        var suffix = tag[(tag.LastIndexOf('_') + 1)..];
        return long.TryParse(suffix, out var tier) ? tier : null;
    }

    /// Port of models/src/data/egogifts.rs get_random_md_ego_gifts. Weighted sample WITH
    /// replacement (Rust samples the distribution `count` times independently, so duplicates
    /// are possible - do not dedupe). Keys off tag.First(), NOT DetermineEgoTier, matching Rust.
    public static List<long> GetRandomMdEgoGifts(int count)
    {
        var eligible = Data.Value.Where(e => e.tag.FirstOrDefault() != "TIER_5").ToList();
        if (eligible.Count == 0) return new List<long>();

        var weights = eligible.Select(e => e.tag.FirstOrDefault() == "TIER_4" ? 1 : 10).ToList();
        var total = weights.Sum();

        var picked = new List<long>(count);
        for (var i = 0; i < count; i++)
        {
            var roll = Random.Shared.Next(total);
            var acc = 0;
            for (var j = 0; j < eligible.Count; j++)
            {
                acc += weights[j];
                if (roll < acc) { picked.Add(eligible[j].id); break; }
            }
        }
        return picked;
    }
}
