using System.Text.Json;
using System.Text.Json.Serialization;
using OpenLethe.Resources;

namespace OpenLethe.Server.Locale;

/// Port of lethe-server/models/src/data/skill.rs. Three lazily-built lookups over
/// the bundled skill data, used only to assemble few-shot examples for /misc/locale:
///
///   SkillJson    id -> the skill stripped to what a translator needs
///   LocaleJson   id -> the shipped English strings for that skill
///   AbilityIndex scriptName -> buffKeyword -> skill ids using it
///
/// ponytail: eager `Lazy` over the whole 13MB skill tree, exactly as Rust's
/// lazy_static does. First /misc/locale request pays for it; nothing else touches
/// these. Move to a prebuilt index file if startup latency ever matters.
public static class SkillIndex
{
    public const string NoKeyword = "NO_KEYWORD";

    /// Few-shot examples per prompt (Rust truncates to 10).
    private const int Limit = 10;

    private static readonly JsonSerializerOptions Compact = new() { IncludeFields = true };

    private static readonly Lazy<Data> Loaded = new(Build, isThreadSafe: true);

    public static IReadOnlyDictionary<long, string> SkillJson => Loaded.Value.Skills;
    public static IReadOnlyDictionary<long, string> LocaleJson => Loaded.Value.Locales;
    public static IReadOnlyDictionary<string, Dictionary<string, List<long>>> AbilityIndex
        => Loaded.Value.Abilities;

    public static List<SkillTag> Tags =>
        StaticData.GetLocalizeListByPrefix<SkillTag>("en/EN_SkillTag.json");

    /// Rust SkillAbility::find_skills_using_ability. With checkBuffKeyword the
    /// keyword must match too; without it every keyword bucket for the script
    /// counts. Ids missing from either map are dropped - a few-shot example needs
    /// both halves.
    public static List<long> FindSkillsUsingAbility(SkillAbility ability, bool checkBuffKeyword)
    {
        var result = new List<long>();
        if (AbilityIndex.TryGetValue(ability.ScriptName, out var categories))
        {
            if (checkBuffKeyword)
            {
                if (categories.TryGetValue(ability.BuffKeyword ?? NoKeyword, out var ids))
                    result.AddRange(ids);
            }
            else
            {
                foreach (var ids in categories.Values) result.AddRange(ids);
            }
        }

        return result
            .Where(id => SkillJson.ContainsKey(id) && LocaleJson.ContainsKey(id))
            .Distinct().Order().ToList();
    }

    /// Rust locale.rs find_similar_skills: prefer skills sharing an exact ability,
    /// widen to script-name-only matches when that is thin, then pad with random
    /// skills. Capped at 10.
    public static List<long> FindSimilarSkills(CompactSkill skill)
    {
        // Membership is a HashSet and every loop stops at Limit: a common scriptName
        // matches thousands of ids, and Rust's List.contains-per-id scan over all of
        // them is quadratic work for a 10-item answer.
        var result = new List<long>();
        var seen = new HashSet<long> { skill.id };
        var abilities = skill.GetAbilities();

        foreach (var ability in abilities)
        {
            var exact = FindSkillsUsingAbility(ability, checkBuffKeyword: true);
            if (Absorb(exact)) return result;

            // Thin on exact matches - widen to skills sharing the script name only.
            if (exact.Count >= 5) continue;

            foreach (var loose in abilities)
                if (Absorb(FindSkillsUsingAbility(loose, checkBuffKeyword: false))) return result;
        }

        if (result.Count < Limit)
        {
            var pool = AbilityIndex.Values
                .SelectMany(categories => categories.Values)
                .SelectMany(ids => ids)
                .Where(id => !seen.Contains(id))
                .Distinct()
                .ToArray();

            Random.Shared.Shuffle(pool);
            result.AddRange(pool.Take(Limit - result.Count));
        }

        return result;

        // True once the answer is full, so the caller can stop scanning.
        bool Absorb(List<long> ids)
        {
            foreach (var id in ids)
            {
                if (!seen.Add(id)) continue;
                result.Add(id);
                if (result.Count >= Limit) return true;
            }
            return false;
        }
    }

    private static Data Build()
    {
        var skills = new Dictionary<long, string>();
        var abilities = new Dictionary<string, Dictionary<string, List<long>>>();

        // Unlike Rust this reads the tree once and derives both views from it; Rust
        // parses static-data/skill twice, as Value and again as SkillData.
        foreach (var skill in StaticData.GetList<CompactSkill>("static-data/skill"))
        {
            if (!skills.TryAdd(skill.id, JsonSerializer.Serialize(skill, Compact))) continue;

            foreach (var ability in skill.GetAbilities())
            {
                if (string.IsNullOrWhiteSpace(ability.ScriptName)) continue;
                var bucket = abilities.TryGetValue(ability.ScriptName, out var b)
                    ? b
                    : abilities[ability.ScriptName] = new Dictionary<string, List<long>>();
                var ids = bucket.TryGetValue(ability.BuffKeyword ?? NoKeyword, out var l)
                    ? l
                    : bucket[ability.BuffKeyword ?? NoKeyword] = new List<long>();
                if (!ids.Contains(skill.id)) ids.Add(skill.id);
            }
        }

        // Rust re-serializes SkillLocale, which keeps only id + levelList.
        var locales = new Dictionary<long, string>();
        foreach (var entry in StaticData.GetLocalizeListByPrefix<SkillLocale>("en/EN_Skills"))
            locales.TryAdd(entry.id, JsonSerializer.Serialize(entry, Compact));

        return new Data(skills, locales, abilities);
    }

    private sealed record Data(
        Dictionary<long, string> Skills,
        Dictionary<long, string> Locales,
        Dictionary<string, Dictionary<string, List<long>>> Abilities);
}

public readonly record struct SkillAbility(string ScriptName, string? BuffKeyword);

public sealed class SkillTag
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
}

public sealed class SkillLocale
{
    public long id { get; set; }
    public List<JsonElement> levelList { get; set; } = [];
}

/// Rust SkillDataContainer<Value>: a skill reduced to its id and its ability
/// scripts. Serializing this back out IS `skill_to_compact_string`.
public sealed class CompactSkill
{
    public long id { get; set; }
    public List<SkillDatum> skillData { get; set; } = [];

    /// Every distinct (scriptName, buffKeyword) the skill uses, base and coin alike.
    public HashSet<SkillAbility> GetAbilities()
    {
        var found = new HashSet<SkillAbility>();
        foreach (var datum in skillData)
        {
            foreach (var a in datum.abilityScriptList) Add(found, a);
            foreach (var coin in datum.coinList)
                foreach (var a in coin.abilityScriptList) Add(found, a);
        }
        return found;

        static void Add(HashSet<SkillAbility> set, JsonElement ability)
        {
            if (ability.ValueKind != JsonValueKind.Object) return;
            var script = ability.TryGetProperty("scriptName", out var s) ? s.GetString() : null;
            if (script is null) return;

            string? keyword = null;
            if (ability.TryGetProperty("buffData", out var buff)
                && buff.ValueKind == JsonValueKind.Object
                && buff.TryGetProperty("buffKeyword", out var kw))
            {
                keyword = kw.GetString();
            }
            set.Add(new SkillAbility(script, keyword));
        }
    }

    /// Script names under "Modular/" - the DSL whose docs get pulled into the prompt.
    public HashSet<string> GetModularScripts() =>
        GetAbilities()
            .Select(a => a.ScriptName)
            .Where(n => n.StartsWith("modular/", StringComparison.OrdinalIgnoreCase))
            .ToHashSet();
}

public sealed class SkillDatum
{
    // Rust's serde requires both fields; System.Text.Json leaves them empty when a
    // file omits one, so we keep the skill instead of dropping the whole file.
    public List<JsonElement> abilityScriptList { get; set; } = [];
    public List<CoinDatum> coinList { get; set; } = [];
}

public sealed class CoinDatum
{
    public List<JsonElement> abilityScriptList { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter<JobStatus>))]
public enum JobStatus
{
    [JsonStringEnumMemberName("pending")] Pending,
    [JsonStringEnumMemberName("processing")] Processing,
    [JsonStringEnumMemberName("completed")] Completed,
    [JsonStringEnumMemberName("failed")] Failed,
}
