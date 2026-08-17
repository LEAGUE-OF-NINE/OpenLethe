using System.Linq;
using System.Text.Json.Nodes;

namespace OpenLethe.Tests.Replay;

/// Ground-truth railway state, advanced only from CAPTURED responses (never from
/// ours), serialized in the exact shape the RailwaySaveInfo column holds:
/// { "<dungeonId>": { "save": {...}, "nodes": [...], "logs": [...] } }.
public sealed class RailwayTruthState
{
    private readonly JsonObject _state = new();

    public string Json => _state.ToJsonString();

    /// Whether this dungeon's ground truth has been seeded yet. The capture opens
    /// mid-life (three dungeons already in progress), so the records BEFORE the
    /// first GetRailwayDungeonNodeAndLogAll for a dungeon are the seed itself and
    /// cannot be replayed - the harness skips them rather than masking them.
    public bool Knows(long dungeonId) => _state[dungeonId.ToString()] is not null;

    private JsonObject Run(long dungeonId)
    {
        var key = dungeonId.ToString();
        if (_state[key] is JsonObject existing) return existing;
        var made = new JsonObject { ["save"] = new JsonObject(), ["nodes"] = new JsonArray(), ["logs"] = new JsonArray() };
        _state[key] = made;
        return made;
    }

    private static long? DungeonId(JsonNode? req) =>
        (long?)(req?["parameters"]?["dungeonId"] ?? req?["parameters"]?["dungeonid"]);

    public void Advance(string path, JsonNode? req, JsonNode? res)
    {
        if (res?["result"] is not JsonObject result) return;
        var ep = path.Split('/').Last();

        // The combined getter is the only response carrying the whole run - it is
        // what seeds each dungeon's ground truth at the start of the capture.
        if (result["railwaySaveInfo"] is JsonObject all)
        {
            var run = Run((long)all["id"]!);
            run["save"] = all.DeepClone();
            run["nodes"] = (result["nodeDatas"] ?? new JsonArray()).DeepClone();
            run["logs"] = (result["logDatas"] ?? new JsonArray()).DeepClone();
            return;
        }

        JsonObject? target = null;
        if (result["saveInfo"] is JsonObject save)
        {
            target = Run((long)save["id"]!);
            target["save"] = save.DeepClone();
        }
        else if (DungeonId(req) is long id)
        {
            target = Run(id);
        }
        if (target is null) return;

        foreach (var node in (result["updateNodeDatas"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            Upsert(target, node);
        if (result["nodeData"] is JsonObject single) Upsert(target, single);
        if (result["startNodeData"] is JsonObject start) Upsert(target, start);

        // ExitRailwayDungeon echoes neither nodes nor logs, but the capture's next
        // GetRailwayDungeonNodeAndLogAll proves both effects: the clear record is
        // appended, and every node is reset to a bare {nodeid} placeholder.
        if (ep == "ExitRailwayDungeon")
        {
            if ((bool?)result["isclear"] == true && result["currentLog"] is JsonNode log)
                (target["logs"] as JsonArray)!.Add(log.DeepClone());
            target["nodes"] = new JsonArray((target["nodes"] as JsonArray ?? new JsonArray())
                .OfType<JsonObject>()
                .Select(n => (JsonNode)new JsonObject { ["nodeid"] = n["nodeid"]!.DeepClone() })
                .ToArray());
        }
    }

    private static void Upsert(JsonObject run, JsonObject node)
    {
        var nodes = (JsonArray)run["nodes"]!;
        var id = (long)node["nodeid"]!;
        for (var i = 0; i < nodes.Count; i++)
        {
            if ((long?)nodes[i]?["nodeid"] != id) continue;
            nodes[i] = node.DeepClone();
            return;
        }
        nodes.Add(node.DeepClone());
    }
}
