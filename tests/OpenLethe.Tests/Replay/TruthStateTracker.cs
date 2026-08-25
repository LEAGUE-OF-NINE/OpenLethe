using System.Text.Json.Nodes;

namespace OpenLethe.Tests.Replay;

public sealed class TruthStateTracker
{
    private JsonObject _save = new();

    public string MdSaveInfoJson => _save.ToJsonString();

    public void Advance(string path, JsonNode? res)
    {
        var result = res?["result"] as JsonObject;
        if (result is null) return;
        var ep = path.Split('/').Last();

        // 1) full saveInfo / saveinfo → replace whole save
        var full = result["saveInfo"] ?? result["saveinfo"];
        if (full is JsonObject fo) { _save = (JsonObject)fo.DeepClone(); return; }

        // ExitMirrorDungeon's flat {isEndDungeon,isclear,statistics} result isn't a
        // saveInfo/currentInfo echo, but it IS a hidden save side effect (the real server
        // marks the run ended before the following Preview/Acquire calls) - top-level, not
        // under currentInfo, so it needs its own Set-equivalent outside the `cur` block below.
        if (result["isEndDungeon"] is JsonNode ied) _save["isEndDungeon"] = ied.DeepClone();

        // 2) currentInfo only → replace subtree
        if (result["currentInfo"] is JsonObject ci)
        { _save["currentInfo"] = ci.DeepClone(); return; }

        // 3) partial deltas → merge named response keys onto the tracked save
        var cur = _save["currentInfo"] as JsonObject;
        if (cur is null) return;
        void Set(string saveKey, JsonNode? v) { if (v is not null) cur[saveKey] = v.DeepClone(); }

        Set("cost", result["cost"]);
        Set("usedcost", result["usedcost"]);
        Set("cn", result["currentNode"]);
        Set("nr", result["nr"]);
        // EnterMirrorDungeonMapNode's own response never echoes eid (not part of its result
        // shape) - it's a save-only side effect (see the handler comment) that only surfaces
        // via a later full currentInfo/saveInfo echo. Derive it the same way the handler does:
        // look up the entered node (by f/s/nid) in the already-tracked dungeonMap.ns and take
        // its static `eid`.
        if (result["currentNode"] is JsonObject cn && _save["dungeonMap"]?["ns"] is JsonArray ns)
        {
            var match = ns.OfType<JsonObject>().FirstOrDefault(n =>
                n["f"]?.GetValue<long>() == cn["f"]?.GetValue<long>() &&
                n["s"]?.GetValue<long>() == cn["s"]?.GetValue<long>() &&
                n["nid"]?.GetValue<long>() == cn["nid"]?.GetValue<long>());
            if (match?["eid"] is JsonNode eid) cur["eid"] = eid.DeepClone();
        }
        Set("shop", result["shopInfo"]);
        Set("dul", result["dungeonUnitList"]);
        Set("egs", result["egogifts"] ?? result["currentEgoGifts"] ?? result["egoGifts"]);
        Set("pce", result["prevChoiceEvent"]);
        Set("cels", result["cels"]);
        Set("slinfo", result["starlightInfo"]);

        // EnableStartBuffMirrorDungeon's response carries no startBufPoint field to merge
        // generically (the capture never echoes it) - this run's only record enables
        // enableConvertedCost, which the handler zeroes startBufPoint for. Not derivable from
        // the response alone (no request access here), so path-special-cased like the
        // UpgradeEgoGift single-gift merge below.
        if (ep == "EnableStartBuffMirrorDungeon") cur["startBufPoint"] = 0;

        // UpgradeEgoGiftMirrorDungeon returns just the one changed gift as "egoGift" (not
        // a full egogifts list) - merge its ul back into the tracked egs entry by id so a
        // second upgrade on the same gift sees the injected ground-truth ul, not a stale 0.
        if (result["egoGift"] is JsonObject single && single["id"] is JsonNode idNode
            && cur["egs"] is JsonArray egs)
        {
            var id = idNode.GetValue<long>();
            for (var i = 0; i < egs.Count; i++)
            {
                if (egs[i] is JsonObject eo && eo["id"]?.GetValue<long>() == id)
                {
                    egs[i] = single.DeepClone();
                    break;
                }
            }
        }
    }
}
