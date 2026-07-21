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

    private sealed class ValList<T>
    {
        public List<T>? list;
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
