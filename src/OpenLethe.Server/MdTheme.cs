using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using OpenLethe.Resources;

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

// Port of models/src/data/mod.rs get_ab_mirror_rewards_by_node_id - reads ONLY
// battle-mirrordungeon/mirrordungeon-abbattle5-0.json (that folder holds many other stage
// files; a whole-folder scan would pull those in too).
public static class MdAbRewards
{
    private static readonly Lazy<List<AbStage>> Stages =
        new(() => StaticData.GetListFromFile<AbStage>("static-data/battle-mirrordungeon/mirrordungeon-abbattle5-0.json"));

    public static List<AbReward>? GetByNodeId(long encounterId) =>
        Stages.Value.FirstOrDefault(s => s.id == encounterId)?.rewardList;
}
