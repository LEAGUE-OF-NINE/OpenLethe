using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using OpenLethe.Resources;
// Namespace alias (not a type alias) to disambiguate Wire.Element against the global `Element`
// decompiled client-packet type (packets/_shared.cs) - same shape, different type.
using Wire = OpenLethe.Server.Wire;

namespace OpenLethe.Server;

// Static-data record types (camelCase JSON keys; StaticData is case-sensitive, no naming
// policy). Only the fields the map-gen/theme-pool selection logic reads - extra JSON keys
// are ignored. Port of lethe-server models/src/mirror_dungeon/theme.rs + mod.rs.
public sealed class ThemeStatic
{
    public long id;
    public List<ExceptionCondition> exceptionConditions = new();
    public MapGenOption mapGenOption = new();
    public List<long> egoGiftPool = new();
    public List<long> specificEgoGiftPool = new();
    public List<MapGenSequence> mapGenSequence = new();
}

public sealed class ExceptionCondition
{
    public long dungeonIdx;
    public List<long> selectableFloors = new();
}

public sealed class MapGenOption
{
    public List<long> bossPool = new();
    public List<long> battlePool = new();
    public List<long> abBattlePool = new();
    public List<long> hardBattlePool = new();
    public List<long> hardAbBattlePool = new();
    public List<long> eventPool = new();
    public List<long>? specialEventPool;
    public double? specialEventProb;
}

public sealed class MapGenSequence
{
    [JsonPropertyName("type")] public string type_ = "";
    public List<long> numberList = new();
    public List<NodeList> nodeList = new();
}

public sealed class NodeList
{
    public long sector;
    public long idx;
    public long encounterType;
    public long encounterId;
    public List<long> connectedNextNodeIdxList = new();
}

public sealed class DungeonEgoGiftDropPool
{
    public long dungeonId;
    public List<long> egoGifts = new();
    public List<long> excludeEgoGifts = new();
}

public static class MdThemeData
{
    // ponytail: memoize the folder scans - handlers call these per-request; data is static.
    private static readonly Lazy<List<ThemeStatic>> Themes =
        new(() => StaticData.GetList<ThemeStatic>("static-data/mirrordungeon-theme-floor"));
    private static readonly Lazy<List<DungeonEgoGiftDropPool>> Pools =
        new(() => StaticData.GetList<DungeonEgoGiftDropPool>("static-data/mirrordungeon-egogift-droppool"));

    public static List<ThemeStatic> AllThemes() => Themes.Value;

    public static ThemeStatic? GetThemeById(long id) => Themes.Value.FirstOrDefault(t => t.id == id);

    public static List<DungeonEgoGiftDropPool> DropPools() => Pools.Value;
}

// Static per-floor enemy-buff candidate pool (992xxx ids), keyed on cn.f (the floor being
// cleared). Sourced from mirrordungeon-07-extreme.json list[0].floors[cn.f].enemyBuffPool.
// GetEgogiftWithEnemyBuf.pool_v2 is a random subset of this (masked in replay); the source
// set is byte-guarded by a unit test.
public sealed class MdDungeonFloorInfo
{
    public List<long> enemyBuffPool = new();
}

public sealed class MdDungeonInfo
{
    public List<MdDungeonFloorInfo> floors = new();
    public MdDungeonShopInfo shop = new();
}

// One personality-upgrade option offered by the MD shop. `index` == the request's idx;
// `price` is the cost in module points (detectingPrice/detectingStarLight cover the
// isDetected/useStarlight paths, unused by this capture). Sourced from
// mirrordungeon-07-extreme.json list[0].shop.upgradePersonality.
public sealed class MdUpgradePersonalityOption
{
    public long index;
    public long price;
    public long detectingPrice;
    public long detectingStarLight;
}

public sealed class MdDungeonShopInfo
{
    public List<MdUpgradePersonalityOption> upgradePersonality = new();
}

public static class MdUpgradePersonalityCost
{
    private static readonly Lazy<List<MdUpgradePersonalityOption>> Options = new(() =>
    {
        var lst = StaticData.GetListFromFile<MdDungeonInfo>("static-data/mirrordungeon/mirrordungeon-07-extreme.json");
        return lst.Count > 0 ? lst[0].shop.upgradePersonality : new List<MdUpgradePersonalityOption>();
    });

    public static MdUpgradePersonalityOption? ForIndex(long idx) =>
        Options.Value.FirstOrDefault(o => o.index == idx);
}

public static class MdEnemyBuffPool
{
    private static readonly Lazy<List<MdDungeonFloorInfo>> Floors = new(() =>
    {
        var lst = StaticData.GetListFromFile<MdDungeonInfo>("static-data/mirrordungeon/mirrordungeon-07-extreme.json");
        return lst.Count > 0 ? lst[0].floors : new List<MdDungeonFloorInfo>();
    });

    public static List<long> ForFloor(long cnf) =>
        cnf >= 0 && cnf < Floors.Value.Count ? Floors.Value[(int)cnf].enemyBuffPool : new List<long>();
}

// MD floor-boundary constraints (995xxx ids). GetConstraints on an e==6 exit takes every
// entry whose flooridx == cn.f + 1, in file order, unfiltered. Only floors 10-14 define any.
public sealed class MdConstraintEntry
{
    public long id;
    public long flooridx;
}

public sealed class MdConstraintFile
{
    public List<MdConstraintEntry> constraints = new();
}

public static class MdConstraints
{
    private static readonly Lazy<List<MdConstraintEntry>> All = new(() =>
    {
        var lst = StaticData.GetListFromFile<MdConstraintFile>("static-data/mirrordungeon-constraint/mirrordungeon-constraint-07.json");
        return lst.Count > 0 ? lst[0].constraints : new List<MdConstraintEntry>();
    });

    public static List<long> ForFloor(long flooridx) =>
        All.Value.Where(c => c.flooridx == flooridx).Select(c => c.id).ToList();
}

// EGO id -> skill-slot index. A unit's es[*].idx is the EGO's GRADE ordinal (ZAYIN=0, TETH=1,
// HE=2, WAW=3, ALEPH=4), not its position in the request's ego list - so a unit that owns a WAW
// but no HE lands its egos at idx 0/1/3 (capture-verified: run-2 seq6 dul[4] ZAYIN/TETH/WAW).
// Grade read from each EGO's egoType across the whole ego folder (verified: 0 idx mismatches over
// all 65 formation egos in both captured runs).
public sealed class EgoGradeEntry
{
    public long id;
    public string egoType = "";
}

public static class MdEgoGrades
{
    private static readonly Dictionary<string, int> Order = new()
        { ["ZAYIN"] = 0, ["TETH"] = 1, ["HE"] = 2, ["WAW"] = 3, ["ALEPH"] = 4 };

    private static readonly Lazy<Dictionary<long, int>> Map = new(() =>
    {
        var d = new Dictionary<long, int>();
        foreach (var e in StaticData.GetList<EgoGradeEntry>("static-data/ego"))
            if (Order.TryGetValue(e.egoType, out var g)) d[e.id] = g;
        return d;
    });

    public static int SlotFor(long egoId) => Map.Value.TryGetValue(egoId, out var g) ? g : 0;
}

// MD exit-reward table (mirrordungeon-07-extreme.json list[0].rewardInfo.
// exitRewardListByCondition) - a per-floor {clearedfloorIndex[, hardCountOfThisWeek]} tiered
// reward list. `rewardElements` reuses the Wire `Element` {type,id,num} shape (StaticData's
// JsonPropertyName("type") on `type_` binds it directly, no separate static type needed).
public sealed class MdExitRewardCondition
{
    public long clearedfloorIndex;
    public long? hardCountOfThisWeek;
}

public sealed class MdExitRewardConsumption
{
    public long bonusChanceConsumption;
    public long hardChanceConsumption;
    public long moduleConsumption;
}

public sealed class MdExitRewardTier
{
    public long rewardId;
    public MdExitRewardCondition condition = new();
    public MdExitRewardConsumption consumption = new();
    public List<Wire.Element> rewardElements = new();
}

public sealed class MdExitRewardInfo
{
    public List<MdExitRewardTier> exitRewardListByCondition = new();
}

public sealed class MdExitRewardDungeon
{
    public MdExitRewardInfo rewardInfo = new();
}

public static class MdExitRewardTable
{
    private static readonly Lazy<List<MdExitRewardTier>> Tiers = new(() =>
    {
        var lst = StaticData.GetListFromFile<MdExitRewardDungeon>("static-data/mirrordungeon/mirrordungeon-07-extreme.json");
        return lst.Count > 0 ? lst[0].rewardInfo.exitRewardListByCondition : new List<MdExitRewardTier>();
    });

    public static List<MdExitRewardTier> All => Tiers.Value;
}

// PreviewMirrorDungeonExitReward/AcquireMirrorDungeonExitReward's shared reward-computation
// engine. Derived from the md-extreme capture (seq320/321): the 4 offered options are NOT a
// single-tier lookup keyed by the run's actual cleared floor - they SUM every tier row in the
// table (across all 5 defined floors, clearedfloorIndex 0-winFloorIdx) whose
// hardCountOfThisWeek matches a bucket (absent/0 for option 0, ==1 for the "unit" bucket),
// merging rewardElements by (type,id) in first-seen order, then options 1-3 are that unit sum
// SCALED by the option's own index (2x/3x) - moduleConsumption follows the same rule
// (base row's value flat for option 0, unit row's value * index for 1-3). Capture-verified
// exactly for all 4 captured options (see task-exitreward-report.md). NOT floor-selected: this
// run only actually cleared 1 of the extreme mode's 5 floors (lastclearedFloor 10 = local floor
// 0), yet the reward already sums all 5 - so the table isn't gated by progress at all, at least
// not observably from this one capture.
public static class MdExitReward
{
    // ponytail: always exactly 4 options (chanceConsumption 0-3), matching the one captured
    // run. A real chance-economy might offer fewer once the player's remaining bonus/hard
    // chance currency (chanceList ids 10016/10017) runs out - unexercised by the capture, not
    // modelled.
    public static List<Wire.ExitRewardOption> BuildOptions()
    {
        var rows = MdExitRewardTable.All;
        var baseModule = rows.FirstOrDefault(r => (r.condition.hardCountOfThisWeek ?? 0) == 0)?.consumption.moduleConsumption ?? 0;
        var unitModule = rows.FirstOrDefault(r => r.condition.hardCountOfThisWeek == 1)?.consumption.moduleConsumption ?? 0;
        var baseElements = SumBucket(rows, 0);
        var unitElements = SumBucket(rows, 1);

        var options = new List<Wire.ExitRewardOption>();
        for (long i = 0; i <= 3; i++)
        {
            var list = i == 0 ? baseElements : Scale(unitElements, i);
            // The enkephalin-module refund: ITEM 20041, num == mdpassOriginalAmount, appended
            // after the table-derived elements, CONSTANT across all 4 options (not scaled by
            // i - capture-verified: 150 on every option). mdpassOriginalAmount is a weekly
            // chance-economy balance (chanceList id 10018) this codebase does not track -
            // masked in the replay (see ReplayMasks); 0 here is a placeholder.
            list.Add(new Wire.Element { type_ = "ITEM", id = 20041, num = 0 });
            options.Add(new Wire.ExitRewardOption
            {
                chanceConsumption = i,
                rewardList = list,
                starlightConsumption = 0, // masked economy scalar (constant 24 in the capture, source undetermined - see report)
                moduleConsumption = i == 0 ? baseModule : unitModule * i,
                mdpassOriginalAmount = 0, // masked
                mdpassCurrentChanceUsage = 0, // masked
            });
        }
        return options;
    }

    private static List<Wire.Element> SumBucket(List<MdExitRewardTier> rows, long hardCount)
    {
        var order = new List<Wire.Element>();
        var index = new Dictionary<(string, long), Wire.Element>();
        foreach (var row in rows)
        {
            if ((row.condition.hardCountOfThisWeek ?? 0) != hardCount) continue;
            foreach (var el in row.rewardElements)
            {
                var key = (el.type_, el.id);
                if (index.TryGetValue(key, out var existing)) existing.num += el.num;
                else
                {
                    var copy = new Wire.Element { type_ = el.type_, id = el.id, num = el.num };
                    index[key] = copy;
                    order.Add(copy);
                }
            }
        }
        return order;
    }

    private static List<Wire.Element> Scale(List<Wire.Element> src, long factor) =>
        src.Select(e => new Wire.Element { type_ = e.type_, id = e.id, num = e.num * factor }).ToList();
}
