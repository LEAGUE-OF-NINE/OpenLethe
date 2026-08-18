using System.Reflection;
using System.Text.Json;

namespace OpenLethe.Resources;

/// Port of Rust models/src/resources.rs get_static_data<T>. Reads embedded
/// StaticData JSON files: each file is { "list": [ T, ... ] }; a folder's files
/// are flattened into one list. Folder is relative to the StaticData root.
public static class StaticData
{
    private static readonly Assembly Asm = typeof(StaticData).Assembly;

    private static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
    };

    public static List<T> GetList<T>(string folder)
    {
        var prefix = folder.Replace('\\', '/').TrimEnd('/') + "/";
        var result = new List<T>();

        foreach (var name in Asm.GetManifestResourceNames())
        {
            var norm = name.Replace('\\', '/');
            if (!norm.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (!norm.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

            using var stream = Asm.GetManifestResourceStream(name)!;
            ValList<T>? doc;
            try { doc = JsonSerializer.Deserialize<ValList<T>>(stream, Options); }
            catch (JsonException) { continue; } // mirror Rust: warn + skip bad files
            if (doc?.list is not null) result.AddRange(doc.list);
        }
        return result;
    }

    /// Like GetList, but matches the ONE embedded resource whose normalized name ends with
    /// resourcePath (e.g. a specific file within a folder that holds several JSON files).
    /// Mirrors Rust get_static_data(path-to-single-file). Empty if not found / bad json.
    public static List<T> GetListFromFile<T>(string resourcePath)
    {
        var suffix = resourcePath.Replace('\\', '/');

        foreach (var name in Asm.GetManifestResourceNames())
        {
            var norm = name.Replace('\\', '/');
            if (!norm.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;

            using var stream = Asm.GetManifestResourceStream(name)!;
            ValList<T>? doc;
            try { doc = JsonSerializer.Deserialize<ValList<T>>(stream, Options); }
            catch (JsonException) { return new List<T>(); }
            return doc?.list ?? new List<T>();
        }
        return new List<T>();
    }

    /// Port of Rust get_static_data_unlisted<T>: same folder scan as GetList, but each
    /// file IS one T (no { "list": [...] } wrapper).
    public static List<T> GetUnlisted<T>(string folder)
    {
        var prefix = folder.Replace('\\', '/').TrimEnd('/') + "/";
        var result = new List<T>();

        foreach (var name in Asm.GetManifestResourceNames())
        {
            var norm = name.Replace('\\', '/');
            if (!norm.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (!norm.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

            using var stream = Asm.GetManifestResourceStream(name)!;
            T? doc;
            try { doc = JsonSerializer.Deserialize<T>(stream, Options); }
            catch (JsonException) { continue; } // mirror Rust: warn + skip bad files
            if (doc is not null) result.Add(doc);
        }
        return result;
    }

    /// Port of Rust get_embed_data::<Localize, DataList<T>>: a Localize file is one
    /// { "dataList": [ T, ... ] } document. Path is relative to the Localize root.
    public static List<T> GetLocalizeList<T>(string resourcePath)
    {
        var name = "Localize/" + resourcePath.Replace('\\', '/').TrimStart('/');
        using var stream = Asm.GetManifestResourceStream(name);
        if (stream is null) return new List<T>();

        try { return JsonSerializer.Deserialize<DataList<T>>(stream, Options)?.dataList ?? new List<T>(); }
        catch (JsonException) { return new List<T>(); }
    }

    /// Port of Rust get_embed_data::<Localize, DataList<T>>(folder): Rust matches by
    /// path PREFIX, not by folder, so "en/EN_Skills" pulls in EN_Skills.json AND every
    /// EN_Skills_*.json beside it. Files that fail to parse are skipped, as in Rust.
    public static List<T> GetLocalizeListByPrefix<T>(string prefix)
    {
        var full = "Localize/" + prefix.Replace('\\', '/').TrimStart('/');
        var result = new List<T>();

        foreach (var name in Asm.GetManifestResourceNames())
        {
            var norm = name.Replace('\\', '/');
            if (!norm.StartsWith(full, StringComparison.OrdinalIgnoreCase)) continue;
            if (!norm.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

            using var stream = Asm.GetManifestResourceStream(name)!;
            try
            {
                var doc = JsonSerializer.Deserialize<DataList<T>>(stream, Options);
                if (doc?.dataList is not null) result.AddRange(doc.dataList);
            }
            catch (JsonException) { }
        }
        return result;
    }

    private sealed class ValList<T>
    {
        public List<T>? list;
    }

    private sealed class DataList<T>
    {
        public List<T>? dataList;
    }
}

/// { "id": <n> } — the shape most static-data entries share.
public sealed class IdStruct
{
    public long id;
}

/// { "nodeid": <n> } — stagenodereward entries.
public sealed class NodeIdStruct
{
    public long nodeid;
}
