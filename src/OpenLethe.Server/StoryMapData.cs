using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using OpenLethe.Resources;

namespace OpenLethe.Server;

// Static-data record types (camelCase JSON; extra fields ignored). Ports of the Rust
// story_map / ab_battle structs.
public sealed class StoryMap
{
    public long id;
    public List<StoryFloor> floors = new();
}

public sealed class StoryFloor
{
    public long count;
    public List<StorySector> sectors = new();
}

public sealed class StorySector
{
    public long sectorNumber;
    public List<StoryMapNode> nodes = new();
}

public sealed class StoryMapNode
{
    public string id = "";
    public string encounter = "";
    public long encounterId;
    public string isHidden = "";
}

public sealed class AbStage
{
    public long id;
    public List<AbReward> rewardList = new();
}

public sealed class AbReward
{
    [JsonPropertyName("type")] public string rewardType = "";
    public long rewardId;
    public long num;
    public float prob;
}

/// Ports of models/src/data/mod.rs story-map lookups over embedded StaticData.
public static class StoryMapData
{
    // ponytail: memoize the folder scans - each handler calls these; data is static.
    private static readonly Lazy<List<StoryMap>> Maps =
        new(() => StaticData.GetList<StoryMap>("static-data/dungeonMap"));
    private static readonly Lazy<List<AbStage>> AbStages =
        new(() => StaticData.GetList<AbStage>("static-data/battle-ab"));

    public static StoryMap? GetStoryMapById(long mapId) =>
        Maps.Value.FirstOrDefault(m => m.id == mapId);

    private static StoryMapNode? FindNode(long mapId, string nodeId) =>
        GetStoryMapById(mapId)?.floors
            .SelectMany(f => f.sectors).SelectMany(s => s.nodes)
            .FirstOrDefault(n => n.id == nodeId);

    public static long? GetStoryFloorEncounterId(long mapId, string nodeId) =>
        FindNode(mapId, nodeId)?.encounterId;

    public static StoryMapNode? GetStoryNodeById(long mapId, string nodeId) =>
        FindNode(mapId, nodeId);

    public static List<AbReward>? GetAbRewardsByNodeId(long mapId, string nodeId)
    {
        var encounterId = GetStoryFloorEncounterId(mapId, nodeId);
        if (encounterId is null) return null;
        return AbStages.Value.FirstOrDefault(s => s.id == encounterId)?.rewardList;
    }
}
