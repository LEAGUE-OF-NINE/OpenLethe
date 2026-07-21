using System.Text.Json;

namespace OpenLethe.Server;

/// Typed access to the Account's opaque jsonb string columns, using the shared
/// wire serializer. Neutral cycle-2 defaults ("{}", "[]", "", null) read as absent.
public static class AccountFields
{
    public static T? Get<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        var trimmed = json.Trim();
        if (trimmed is "{}" or "[]" or "null") return default;

        try { return JsonSerializer.Deserialize<T>(trimmed, global::PacketJson.Options); }
        catch (JsonException) { return default; }
    }

    public static string Set<T>(T value) =>
        JsonSerializer.Serialize(value, global::PacketJson.Options);

    /// Upsert by key: each incoming element replaces the existing element with the
    /// same key, or is appended. Mirrors Rust update_egos/update_personalities.
    public static List<T> MergeById<T, TKey>(List<T> existing, List<T> incoming, Func<T, TKey> key)
        where TKey : notnull
    {
        var index = new Dictionary<TKey, int>();
        var result = new List<T>(existing);
        for (var i = 0; i < result.Count; i++) index[key(result[i])] = i;

        foreach (var item in incoming)
        {
            if (index.TryGetValue(key(item), out var i)) result[i] = item;
            else { index[key(item)] = result.Count; result.Add(item); }
        }
        return result;
    }
}
