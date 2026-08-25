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

// static-data/mirror-dungeon-common-data/*.json (unlisted, one object per file) - same
// folder MdEgoFusion's combine tables come from. Rust models/src/mirror_dungeon/ego.rs
// EgoGiftUpgradeCostTable. Also carries starlightInfo (read separately below for the
// start-of-run buff-point bonus).
public sealed class EgoGiftUpgradeCostTableFile
{
    public UpgradeCostTableData? egoGiftUpgradeCostTable;
    public MdCommonStarlightInfo? starlightInfo;
}

public sealed class MdCommonStarlightInfo
{
    public long detectThemeFloorDefaultPoint;
}

public sealed class UpgradeCostTableData
{
    public List<UpgradeCostTierRow> table = new();
}

public sealed class UpgradeCostTierRow
{
    public long tier;
    public List<long> cost = new();
}

public static class MdEgoData
{
    // ponytail: memoize the folder scan - handlers call this per-request; data is static.
    private static readonly Lazy<List<MdEgo>> Data =
        new(() => StaticData.GetList<MdEgo>("static-data/ego-gift-mirrordungeon"));

    private static readonly Lazy<UpgradeCostTableData?> UpgradeCostTable = new(() =>
        StaticData.GetUnlisted<EgoGiftUpgradeCostTableFile>("static-data/mirror-dungeon-common-data")
            .Select(t => t.egoGiftUpgradeCostTable)
            .FirstOrDefault(t => t is not null));

    // SelectFormation's start-buff-point bonus (60 base + this = capture's 80). Sourced from
    // the same mirror-dungeon-common-data-md7.json file's starlightInfo.detectThemeFloorDefaultPoint -
    // it's the same "default point grant" constant the starlight system reuses (byte-confirmed
    // equal to the observed +20 bonus; only one capture record exercises this).
    private static readonly Lazy<long> DetectThemeFloorDefaultPointValue = new(() =>
        StaticData.GetUnlisted<EgoGiftUpgradeCostTableFile>("static-data/mirror-dungeon-common-data")
            .Select(t => t.starlightInfo)
            .FirstOrDefault(t => t is not null)?.detectThemeFloorDefaultPoint ?? 0);

    public static long DetectThemeFloorDefaultPoint => DetectThemeFloorDefaultPointValue.Value;

    // First-wins on duplicate id, preserving the old FirstOrDefault scan's answer.
    private static readonly Lazy<Dictionary<long, MdEgo>> ById = new(() =>
    {
        var d = new Dictionary<long, MdEgo>();
        foreach (var e in Data.Value) d.TryAdd(e.id, e);
        return d;
    });

    public static MdEgo? GetById(long id) => ById.Value.GetValueOrDefault(id);

    public static List<long> AllIds() => Data.Value.Select(e => e.id).ToList();

    public static long UpgradeCost(long price, long desiredUl) => ((price * desiredUl / 3) / 10) * 10;

    // Port of the classic MD upgrade_ego_gift_mirror_dungeon.rs cost lookup: a flat
    // per-tier table (mirror-dungeon-common-data-md7.json egoGiftUpgradeCostTable),
    // NOT the price-based UpgradeCost formula above (that one's still what Story-MD
    // uses - untouched here). cost[desiredUl-1] for the gift's DetermineEgoTier.
    // Byte-verified against 11/12 captured records - see task-12-report.md for the
    // one holdout (gift 9055, seq289: table predicts 100, capture shows 70).
    public static long TierUpgradeCost(long egoGiftId, long desiredUl)
    {
        var tier = DetermineEgoTier(egoGiftId);
        var row = UpgradeCostTable.Value?.table.FirstOrDefault(t => t.tier == tier);
        if (row is null || desiredUl < 1 || desiredUl > row.cost.Count) return 0;
        return row.cost[(int)desiredUl - 1];
    }

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
