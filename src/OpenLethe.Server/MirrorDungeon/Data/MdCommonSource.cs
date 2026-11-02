using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenLethe.Resources;

namespace OpenLethe.Server.MirrorDungeon.Data;

// mirror-dungeon-common-data: the season's one big table (hidden battles, fusion recipes and tiers,
// upgrade and victory costs, start gift pools, shop constants, ...). Every reader - MdCommonTable,
// MdEgoFusion, MdEgoData - takes its typed view of it from Current, so an account's upload
// (POST /custom/upload/mirror-dungeon-common-data, carried by MdCatalog) changes every rule at once
// and no upload leaves the static file in charge.
//
// The static source is the current season's file (currentDungeonId == StartRunRules.CurrentDungeonId).
// The md7 and md8 files agree on every section the server reads, so readers that used to take the
// first file or all files read exactly what they did before.
//
// An upload lays its top-level keys over that file; a key it leaves out keeps the static value, and
// its currentDungeonId is ignored (only the current season is ever read). A key it sets replaces the
// static one wholesale, except two sections that merge field by field so a mod can ADD to them:
//   hiddenBattleInfo          - pool gains the uploaded stages; minFloorCondition / probInfo replace.
//   egoGiftCombineFixedTable  - combineFixed / combineMixed / nonAcquireableInEasyIds gain the
//                               uploaded entries they lack.
// Several uploaded files apply in order.
public sealed class MdCommonSource
{
    private const string Folder = "static-data/mirror-dungeon-common-data";

    private static readonly JsonSerializerOptions Options = new() { IncludeFields = true };

    // The typed views the server reads. An upload must deserialize into each of them.
    private static readonly Type[] ViewTypes =
    {
        typeof(MdCommonData), typeof(EgoGiftUpgradeCostTableFile),
        typeof(EgoGiftCombineFixedTable), typeof(EgoGiftCombineTierTable),
    };

    // Field-by-field sections and the arrays in them that are added to rather than replaced.
    private static readonly Dictionary<string, string[]> AdditiveSections = new()
    {
        ["hiddenBattleInfo"] = new[] { "pool" },
        ["egoGiftCombineFixedTable"] = new[] { "combineFixed", "combineMixed", "nonAcquireableInEasyIds" },
    };

    private static readonly Lazy<MdCommonSource> StaticSource = new(() => new MdCommonSource(StaticFile()));

    // Built sources by the account column they came from, so a request does not re-parse and
    // re-derive an upload it has seen before. Cleared wholesale when it grows past a bound.
    private static readonly ConcurrentDictionary<string, MdCommonSource> ByColumn = new();
    private const int ByColumnLimit = 256;

    public static MdCommonSource Static => StaticSource.Value;

    // The request's account upload laid over the static file, or the static file alone.
    public static MdCommonSource Current => MdCatalog.Current?.Common ?? Static;

    private readonly string json;
    private readonly JsonElement root;
    private readonly ConcurrentDictionary<string, object> cache = new();

    private MdCommonSource(JsonObject file)
    {
        json = file.ToJsonString();
        using var doc = JsonDocument.Parse(json);
        root = doc.RootElement.Clone();
    }

    // The file as T (memoized). An upload that cannot be read as T falls back to the static view.
    public T View<T>() where T : class, new() =>
        (T)cache.GetOrAdd("view:" + typeof(T).FullName, _ =>
        {
            try { return root.Deserialize<T>(Options) ?? new T(); }
            catch (JsonException) { return ReferenceEquals(this, Static) ? new T() : Static.View<T>(); }
        });

    // A value computed from this source (memoized under key) - for derived tables like the fusion
    // recipe hash map, which must never be shared between the static file and an upload.
    public T Derived<T>(string key, Func<MdCommonSource, T> build) where T : class =>
        (T)cache.GetOrAdd("derived:" + key, _ => build(this));

    // The uploads laid over the static file; null when there are none.
    public static MdCommonSource? Overlay(IEnumerable<JsonObject?>? uploads)
    {
        var list = uploads?.OfType<JsonObject>().ToList();
        if (list is null || list.Count == 0) return null;

        var merged = JsonNode.Parse(Static.json)!.AsObject();
        foreach (var upload in list) Apply(merged, upload);
        return new MdCommonSource(merged);
    }

    // Account.CustomMdCommonData (a list of uploaded files) as a source; null for none.
    public static MdCommonSource? FromColumn(string? column)
    {
        var uploads = AccountFields.Get<List<JsonObject?>>(column);
        if (uploads is not { Count: > 0 }) return null;
        if (ByColumn.TryGetValue(column!, out var cached)) return cached;

        var built = Overlay(uploads);
        if (built is null) return null;
        if (ByColumn.Count >= ByColumnLimit) ByColumn.Clear();
        ByColumn[column!] = built;
        return built;
    }

    // Why this upload cannot be used (a section of the wrong shape), or null when it can.
    public static string? Validate(JsonObject upload)
    {
        var merged = JsonNode.Parse(Static.json)!.AsObject();
        Apply(merged, upload);
        var element = JsonSerializer.SerializeToElement(merged);
        foreach (var type in ViewTypes)
        {
            try { element.Deserialize(type, Options); }
            catch (JsonException e) { return e.Path is { } path ? $"{path}: {e.Message}" : e.Message; }
        }
        return null;
    }

    private static void Apply(JsonObject into, JsonObject upload)
    {
        foreach (var (key, value) in upload)
        {
            if (key == "currentDungeonId" || value is null) continue;
            if (AdditiveSections.TryGetValue(key, out var additive) && value is JsonObject section
                && into[key] is JsonObject existing)
            {
                MergeSection(existing, section, additive);
                continue;
            }
            into[key] = value.DeepClone();
        }
    }

    // Field by field: an additive array gains the uploaded entries it does not already hold, any
    // other field is replaced.
    private static void MergeSection(JsonObject into, JsonObject upload, string[] additive)
    {
        foreach (var (field, value) in upload)
        {
            if (value is JsonArray add && additive.Contains(field) && into[field] is JsonArray existing)
            {
                foreach (var item in add)
                    if (!existing.Any(e => JsonNode.DeepEquals(e, item))) existing.Add(item?.DeepClone());
                continue;
            }
            into[field] = value?.DeepClone();
        }
    }

    private static JsonObject StaticFile()
    {
        var files = StaticData.GetUnlisted<JsonObject>(Folder);
        return files.FirstOrDefault(f => f["currentDungeonId"] is JsonValue v && v.TryGetValue<long>(out var id)
                   && id == Rules.StartRunRules.CurrentDungeonId)
               ?? files.FirstOrDefault()
               ?? new JsonObject();
    }
}
