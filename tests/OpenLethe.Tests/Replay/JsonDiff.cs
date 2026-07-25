using System.Text.Json.Nodes;

namespace OpenLethe.Tests.Replay;

public static class JsonDiff
{
    public static List<string> Compare(JsonNode? ours, JsonNode? theirs, string[] masks)
    {
        var diffs = new List<string>();
        Walk("", ours, theirs, masks, diffs);
        return diffs;
    }

    static bool Masked(string path, string[] masks)
    {
        foreach (var m in masks) if (MaskMatch.Matches(m, path)) return true;
        return false;
    }

    // Array-element path segment. Elements that are JSON objects with a string `rt` property
    // (reward-event entries in currentInfo.rre) get an `[rt=<value>]` segment instead of the
    // positional `[i]`, so a mask can target one reward TYPE's RNG pool while the deterministic
    // pools of sibling types (at varying indices) stay byte-verified. Comparison is still
    // positional (aa[i] vs ba[i]); two entries sharing an rt get the same path, which is
    // harmless because those pools are never masked. Everything else keeps `[i]`.
    static string ArraySeg(JsonNode? el, int i)
    {
        if (el is JsonObject o && o["rt"] is JsonValue v && v.TryGetValue<string>(out var rt))
            return $"[rt={rt}]";
        return $"[{i}]";
    }

    static void Walk(string path, JsonNode? a, JsonNode? b, string[] masks, List<string> diffs)
    {
        if (Masked(path, masks)) return;

        if (a is JsonObject oa && b is JsonObject ob)
        {
            foreach (var key in oa.Select(k => k.Key).Union(ob.Select(k => k.Key)))
            {
                var child = path.Length == 0 ? key : $"{path}.{key}";
                Walk(child, oa[key], ob[key], masks, diffs);
            }
            return;
        }
        if (a is JsonArray aa && b is JsonArray ba)
        {
            if (aa.Count != ba.Count && !Masked(path, masks)) { diffs.Add(path + ".<length>"); return; }
            for (var i = 0; i < System.Math.Min(aa.Count, ba.Count); i++)
                Walk($"{path}{ArraySeg(aa[i], i)}", aa[i], ba[i], masks, diffs);
            return;
        }
        // leaf compare (null-safe, by JSON text)
        var sa = a?.ToJsonString(); var sb = b?.ToJsonString();
        if (sa != sb) diffs.Add(path.Length == 0 ? "<root>" : path);
    }
}
