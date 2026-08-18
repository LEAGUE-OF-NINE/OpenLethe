using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using OpenLethe.Server.Auth;

namespace OpenLethe.Server.Locale;

/// Port of lethe-server/server/src/locale.rs - generates English skill text for a
/// modded skill by prompting an OpenAI-compatible chat model with the game's own
/// skill/locale pairs as few-shot examples.
///
/// Three routes: /misc/locale runs it inline, /misc/locale/submit queues it and
/// /misc/locale/result/{id} collects the answer. Both entry points require the
/// abuse_exemption captcha cookie and are rate limited per user.
public static partial class LocaleEndpoints
{
    private static readonly TimeSpan RateLimit = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan JobTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan ModularDocTtl = TimeSpan.FromMinutes(5);

    public static IEndpointRouteBuilder MapLocale(this IEndpointRouteBuilder app)
    {
        app.MapPost("/misc/locale", async (
            HttpContext ctx, IConfiguration cfg, IHttpClientFactory http, IMemoryCache cache,
            JwtService jwt, CancellationToken ct) =>
        {
            if (await ReadSkillAsync(ctx, ct) is not { } skill) return Results.BadRequest();
            if (Gate(ctx, cache, jwt) is { } rejection) return rejection;

            try
            {
                return Results.Json(await TranslateAsync(cfg, http, cache, skill, ct));
            }
            catch (Exception e)
            {
                return Results.Json(new { error = e.Message }, statusCode: 502);
            }
        });

        app.MapPost("/misc/locale/submit", async (
            HttpContext ctx, IConfiguration cfg, IHttpClientFactory http, IMemoryCache cache,
            JwtService jwt, CancellationToken ct) =>
        {
            if (await ReadSkillAsync(ctx, ct) is not { } skill) return Results.BadRequest();
            if (Gate(ctx, cache, jwt) is { } rejection) return rejection;

            var jobId = Guid.NewGuid().ToString();
            var key = JobKey(jobId);
            cache.Set(key, new TranslationJob { jobId = jobId, status = JobStatus.Pending }, JobTtl);

            // Fire-and-forget, as Rust's tokio::spawn is: the caller polls for the
            // result. No request-scoped services are captured, so nothing can be
            // disposed out from under it - ct is deliberately NOT passed along.
            //
            // Each transition REPLACES the cache entry rather than mutating it, so a
            // poll can never observe a half-written job (status completed, result
            // still null).
            _ = Task.Run(async () =>
            {
                cache.Set(key, new TranslationJob { jobId = jobId, status = JobStatus.Processing }, JobTtl);
                TranslationJob done;
                try
                {
                    done = new TranslationJob
                    {
                        jobId = jobId,
                        status = JobStatus.Completed,
                        result = await TranslateAsync(cfg, http, cache, skill, CancellationToken.None),
                    };
                }
                catch (Exception e)
                {
                    done = new TranslationJob { jobId = jobId, status = JobStatus.Failed, error = e.Message };
                }
                cache.Set(key, done, JobTtl);
            }, CancellationToken.None);

            return Results.Json(new { jobId });
        });

        app.MapGet("/misc/locale/result/{jobId}", (string jobId, IMemoryCache cache) =>
            cache.TryGetValue<TranslationJob>(JobKey(jobId), out var job) && job is not null
                ? Results.Json(job)
                : Results.Json(new { error = "Job not found or expired" }, statusCode: 404));

        return app;
    }

    /// Captcha cookie plus a one-request-per-5s budget. Returns null to proceed.
    private static IResult? Gate(HttpContext ctx, IMemoryCache cache, JwtService jwt)
    {
        var cookie = ctx.Request.Cookies["abuse_exemption"];
        if (cookie is null || !jwt.TryVerifyClaims(cookie, out var claims) || !claims.Captcha)
        {
            return Results.Json(
                new { error = "Captcha validation failed. Please complete the CAPTCHA to proceed." },
                statusCode: 422);
        }

        var key = "locale-rate:" + claims.Sub;
        if (cache.TryGetValue(key, out _))
        {
            return Results.Json(
                new { error = "Rate limit exceeded. Please wait before making another request." },
                statusCode: 429);
        }

        cache.Set(key, true, RateLimit);
        return null;
    }

    private static string JobKey(string id) => "locale-job:" + id;

    /// Null on an unreadable body, so the caller can 400 before spending the
    /// request's rate-limit budget.
    private static async Task<JsonElement?> ReadSkillAsync(HttpContext ctx, CancellationToken ct)
    {
        try { return await ctx.Request.ReadFromJsonAsync<JsonElement>(ct); }
        catch (JsonException) { return null; }
        catch (BadHttpRequestException) { return null; }
    }

    // --- translation ---------------------------------------------------------

    private static async Task<JsonElement> TranslateAsync(
        IConfiguration cfg, IHttpClientFactory http, IMemoryCache cache,
        JsonElement skill, CancellationToken ct)
    {
        var apiKey = cfg["OPENAI_API_KEY"]
            ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable not set");
        var baseUrl = (cfg["OPENAI_BASE_URL"] ?? "https://api.openai.com/v1").TrimEnd('/');
        var model = cfg["OPENAI_MODEL"] ?? "gpt-3.5-turbo";

        var compact = Compact(skill);
        var request = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = await SystemPromptAsync(http, cache, skill, ct) },
                new { role = "user", content =
                    $"Please generate appropriate locale data for this skill:\n\n{compact}" },
            },
            temperature = 0.0,
            max_tokens = 1500,
        };

        var client = http.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
        {
            Content = JsonContent.Create(request),
        };
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

        var res = await client.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"API request failed with status {(int)res.StatusCode}: "
                + await res.Content.ReadAsStringAsync(ct));
        }

        var completion = await res.Content.ReadFromJsonAsync<ChatCompletionResponse>(ct);
        var content = completion?.choices?.FirstOrDefault()?.message?.content
            ?? throw new InvalidOperationException("No response choices received from API");

        return ParseJsonBody(content);
    }

    /// Models like to wrap JSON in prose or a fence, so fall back to the outermost
    /// brace pair before giving up. Same two-step as Rust.
    public static JsonElement ParseJsonBody(string content)
    {
        try { return JsonDocument.Parse(content).RootElement.Clone(); }
        catch (JsonException) { }

        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new InvalidOperationException($"Could not find JSON in response: {content}");

        try { return JsonDocument.Parse(content[start..(end + 1)]).RootElement.Clone(); }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Could not parse response as JSON: {content}");
        }
    }

    private static string Compact(JsonElement skill) =>
        JsonSerializer.Serialize(
            skill.Deserialize<CompactSkill>(new JsonSerializerOptions { IncludeFields = true })
                ?? new CompactSkill(),
            new JsonSerializerOptions { IncludeFields = true });

    public static async Task<string> SystemPromptAsync(
        IHttpClientFactory http, IMemoryCache cache, JsonElement skill, CancellationToken ct)
    {
        var parsed = skill.Deserialize<CompactSkill>(new JsonSerializerOptions { IncludeFields = true })
            ?? new CompactSkill();

        var sb = new StringBuilder("""

    Your task is to translate game skill data into localized text entries.

    Skill abilities are usually named with a scriptName and sometimes a buffKeyword.
    The scriptName indicates the type of ability, but sometimes it starts with the word MODULAR,
    which is a DSL for combining multiple effects. You are given the modular documentation below.

    IMPORTANT:
    Ignore skill abilities like ChangeSkillMotion, ChangeAppearance, they do not need localization.
    If a coin has no abilities, generate an empty 'coindescs': [] array for it,
    coindescs must match the order of the coinList in the skill data.

    # The following skill tags are available for use in skill descriptions:

""");

        foreach (var tag in SkillIndex.Tags) sb.Append($"\n{tag.id} -> {tag.name}");

        sb.Append("\n\n# Modular Documentation:\n");

        // Only skills using the DSL need its docs, and those docs come from a
        // third-party page: skip the fetch when there is nothing to document, and
        // degrade to no docs when rentry.co is down or has changed its markup. A
        // thinner prompt beats a dead endpoint.
        var modularScripts = parsed.GetModularScripts();
        var modularDoc = modularScripts.Count == 0
            ? []
            : await TryFetchModularDocAsync(http, cache, ct);

        foreach (var (key, value) in modularDoc)
            if (modularScripts.Any(s => s.Contains(key, StringComparison.Ordinal)))
                sb.Append($"\n{value}");

        sb.Append("""
---
    # Examples
    Here are examples of skills with their locale data:
    Generate appropriate locale data in the same JSON format as the examples below.

""");

        foreach (var id in SkillIndex.FindSimilarSkills(parsed))
        {
            if (SkillIndex.SkillJson.TryGetValue(id, out var input)
                && SkillIndex.LocaleJson.TryGetValue(id, out var output))
            {
                sb.Append($"\n**input**\n{input}\n**output**\n{output}\n\n");
            }
        }

        return sb.ToString();
    }

    // --- modular documentation ----------------------------------------------

    /// Scrapes the GlitchScript reference off its rentry.co edit page (the raw
    /// markdown lives in that page's textarea) and splits it into one section per
    /// function so only the relevant ones enter the prompt. Rust caches the parse
    /// in a temp file; an in-memory 5-minute entry does the same job.
    private static async Task<Dictionary<string, string>> TryFetchModularDocAsync(
        IHttpClientFactory http, IMemoryCache cache, CancellationToken ct)
    {
        try { return await FetchModularDocAsync(http, cache, ct); }
        catch (HttpRequestException) { return []; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return []; }
        catch (InvalidOperationException) { return []; } // markup changed under us
    }

    internal static async Task<Dictionary<string, string>> FetchModularDocAsync(
        IHttpClientFactory http, IMemoryCache cache, CancellationToken ct)
    {
        if (cache.TryGetValue<Dictionary<string, string>>("modular-doc", out var cached)
            && cached is not null)
        {
            return cached;
        }

        var html = await http.CreateClient().GetStringAsync("https://rentry.co/glitchscript/edit", ct);
        var doc = ParseModularDoc(html);
        cache.Set("modular-doc", doc, ModularDocTtl);
        return doc;
    }

    public static Dictionary<string, string> ParseModularDoc(string html)
    {
        // ponytail: one regex instead of an HTML parser dependency - the page has a
        // single #id_text textarea and we want its raw text, not a DOM.
        var match = TextAreaRegex().Match(html);
        if (!match.Success)
            throw new InvalidOperationException("Could not find #id_text element");

        var text = WebUtility.HtmlDecode(match.Groups[1].Value);

        var parts = text.Split("**Value Acquisition Functions**");
        if (parts.Length != 2)
            throw new InvalidOperationException("Could not split text on '**Value Acquisition Functions**'");

        var doc = new Dictionary<string, string> { [""] = parts[0] };

        string? current = null;
        foreach (var line in parts[1].Split('\n').Select(l => l.TrimEnd('\r')))
        {
            if (line.Contains("**Condition Functions**")) break;

            var header = FunctionHeaderRegex().Match(line);
            if (header.Success)
            {
                current = header.Groups[1].Value;
                doc[current] = line + "\n";
            }
            else if (current is not null && doc.ContainsKey(current))
            {
                doc[current] += line + "\n";
            }
        }

        return doc;
    }

    [GeneratedRegex("""<textarea[^>]*\bid=["']?id_text["']?[^>]*>(.*?)</textarea>""",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TextAreaRegex();

    [GeneratedRegex(@"###\s*\*\*(\w+).*\*\*")]
    private static partial Regex FunctionHeaderRegex();

    // --- wire shapes ---------------------------------------------------------

    private sealed class TranslationJob
    {
        public string jobId { get; set; } = "";
        public JobStatus status { get; set; }
        public JsonElement? result { get; set; }
        public string? error { get; set; }
    }

    private sealed class ChatCompletionResponse { public List<Choice>? choices { get; set; } }
    private sealed class Choice { public ChatMessage? message { get; set; } }
    private sealed class ChatMessage { public string? content { get; set; } }
}
