using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace OpenLethe.Server.Handlers;

/// The files LetheLauncher downloads from the server root before starting the game.
///
/// Each is resolved in three tiers, first hit wins:
///
///   1. A local file in MOD_FILES_DIR. Explicit operator action, so it outranks
///      everything - this is the self-hosting path, no Discord account needed.
///   2. The newest matching attachment in a Discord release channel, as upstream
///      does. Needs DISCORD_TOKEN plus the channel id for that file; we redirect
///      to the CDN rather than proxying the bytes.
///   3. The upstream default: a redirect for limbus-manifest.txt, a canned
///      document for noticeMeta.json, and a 404 for the DLLs, which have no
///      sensible default.
///
/// A Discord outage falls through to tier 3 rather than failing the request.
public static class ModFiles
{
    /// Rust caches these in the 60s ExpiringMap it shares with /misc/avatar.
    /// Discord CDN attachment links are signed and time-limited, so caching the
    /// URL for long would hand out links that have already expired.
    private static readonly TimeSpan UrlTtl = TimeSpan.FromSeconds(60);

    /// Fixed list rather than a catch-all route: it cannot shadow /health or
    /// /serverinfos, and no part of any filename comes from the request, so path
    /// traversal is not expressible.
    /// Channel keys are tried in order, first one configured wins. motions.dll
    /// gets its own so it can be published separately, falling back to the shared
    /// RELEASE_CHANNEL_ID that upstream uses for both.
    private static readonly (string Name, string[] ChannelKeys)[] Served =
    [
        ("Lethe.dll", ["RELEASE_CHANNEL_ID"]),
        ("ModularSkillScripts.dll", ["MODULAR_RELEASE_CHANNEL_ID"]),
        ("motions.dll", ["MOTIONS_RELEASE_CHANNEL_ID", "RELEASE_CHANNEL_ID"]),
        ("limbus-manifest.txt", []),
        ("noticeMeta.json", []),
    ];

    private const string DefaultManifestUrl = "https://files.lethelc.site/limbus-manifest.txt";

    public static IEndpointRouteBuilder MapModFiles(this IEndpointRouteBuilder app)
    {
        var env = app.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var startupCfg = app.ServiceProvider.GetRequiredService<IConfiguration>();

        var configured = startupCfg["MOD_FILES_DIR"] ?? "modfiles";
        var root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);

        foreach (var (name, channelKeys) in Served)
        {
            var path = Path.Combine(root, name);

            app.MapGet("/" + name, async (
                IConfiguration cfg, IHttpClientFactory http, IMemoryCache cache, CancellationToken ct) =>
            {
                if (File.Exists(path)) return Results.File(path, ContentType(name), name);

                var channelId = channelKeys
                    .Select(k => cfg[k])
                    .FirstOrDefault(v => !string.IsNullOrEmpty(v));

                if (!string.IsNullOrEmpty(channelId)
                    && await TryDiscordUrlAsync(http, cache, cfg, channelId, name, ct) is { } url)
                {
                    // 307, matching Rust's Redirect::temporary. The launcher's
                    // reqwest follows it and writes whatever comes back.
                    return Results.Redirect(url, permanent: false, preserveMethod: true);
                }

                return name switch
                {
                    // Rust: Redirect::permanent to a fixed files.lethelc.site URL.
                    // Configurable here so a self-hoster can point somewhere else.
                    "limbus-manifest.txt" => Results.Redirect(
                        Fallback(cfg, "LIMBUS_MANIFEST_URL", DefaultManifestUrl),
                        permanent: true, preserveMethod: true),

                    // Rust serves this inline; there is no file behind it upstream.
                    "noticeMeta.json" => Results.Text(NoticeMeta, "application/json"),

                    _ => Results.Text(
                        $"{name} not found. Put it in {root} (MOD_FILES_DIR), or set "
                        + $"{string.Join(" or ", channelKeys)} and DISCORD_TOKEN to serve it "
                        + "from a release channel.",
                        "text/plain", statusCode: StatusCodes.Status404NotFound),
                };
            })
            // Exempt from the global request timeout: these stream multi-megabyte
            // files, and 15s is a handler budget, not a download budget. Upstream
            // never hits this because it always redirects to the Discord CDN - our
            // local-file tier serves the bytes itself.
            .DisableRequestTimeout();
        }

        return app;
    }

    /// Newest attachment in the channel whose filename matches. Null on any
    /// failure - a missing token, a bad channel, an outage, changed JSON - so the
    /// caller falls through instead of 500ing.
    private static async Task<string?> TryDiscordUrlAsync(
        IHttpClientFactory http, IMemoryCache cache, IConfiguration cfg,
        string channelId, string filename, CancellationToken ct)
    {
        var key = "moddl:" + filename;
        if (cache.TryGetValue<string>(key, out var cached) && cached is not null) return cached;

        var token = cfg["DISCORD_TOKEN"];
        if (string.IsNullOrEmpty(token)) return null;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://discord.com/api/v9/channels/{channelId}/messages");
            req.Headers.TryAddWithoutValidation("Authorization", $"Bot {token}");

            var res = await http.CreateClient().SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;

            var messages = await res.Content.ReadFromJsonAsync<List<DiscordMessage>>(ct);
            var url = messages?
                .SelectMany(m => m.attachments ?? [])
                .FirstOrDefault(a => string.Equals(a.filename, filename, StringComparison.Ordinal))
                ?.url;

            if (url is not null) cache.Set(key, url, UrlTtl);
            return url;
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return null; }
        catch (JsonException) { return null; }
    }

    private static string Fallback(IConfiguration cfg, string key, string fallback) =>
        string.IsNullOrEmpty(cfg[key]) ? fallback : cfg[key]!;

    private static string ContentType(string name) => Path.GetExtension(name) switch
    {
        ".json" => "application/json",
        ".txt" => "text/plain",
        _ => "application/octet-stream",
    };

    private sealed class DiscordMessage { public List<DiscordAttachment>? attachments { get; set; } }
    private sealed class DiscordAttachment
    {
        public string? filename { get; set; }
        public string? url { get; set; }
    }

    /// Verbatim from Rust's handle_meta. The dates are the client's "show no
    /// notices" window, not anything we generate.
    private const string NoticeMeta = """
        {
          "latestUpdateDate": "Wed Mar 11 2026 06:24:32 GMT+0000 (Coordinated Universal Time)",
          "noticeDetailList": [
            {
              "id": 200001,
              "startDate": "2023-01-01T00:00:00.000Z",
              "endDate": "2098-12-31T21:00:00.000Z",
              "fileName_KR": "noticeDetail_200001_KR_219.json",
              "fileName_EN": "noticeDetail_200001_EN_219.json",
              "fileName_JP": "noticeDetail_200001_JP_219.json"
            }
          ]
        }
        """;
}
