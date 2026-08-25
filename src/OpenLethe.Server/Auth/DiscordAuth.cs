using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using OpenLethe.Data;

namespace OpenLethe.Server.Auth;

/// Port of lethe-server/server/src/auth.rs - Discord OAuth login for the launcher
/// and the Crux frontend, plus the Turnstile captcha exemption that gates
/// /misc/locale. The game client never touches these routes; they mint the JWT
/// that later arrives in a userAuth envelope.
///
/// Not ported: the CSRF check Rust also lacks (its own TODO), and the six
/// hard-coded Discord snowflakes in `is_whitelisted` - those are real user IDs
/// and belong in config, not in source. DISCORD_WHITELIST_IDS replaces them.
public static class DiscordAuth
{
    /// Rust: ExpiringMap::new(Duration::from_secs(10)) on AuthState.cache. Short
    /// because a session_id -> token handoff is a single poll away; raise it if a
    /// launcher ever polls slower than this.
    private static readonly TimeSpan HandoffTtl = TimeSpan.FromSeconds(10);

    /// Rust: the 60s ExpiringMap shared with /misc/avatar.
    private static readonly TimeSpan AvatarTtl = TimeSpan.FromSeconds(60);

    private const string DefaultAvatar = "https://cdn.discordapp.com/embed/avatars/0.png";

    public static IEndpointRouteBuilder MapDiscordAuth(this IEndpointRouteBuilder app)
    {
        // Step 1: the launcher/game opens this with its session_id. Plants the
        // cookie that ties the browser round-trip back to the polling client.
        app.MapGet("/auth/login", (HttpContext ctx, IConfiguration cfg) =>
        {
            var q = LoginQuery.From(ctx.Request.Query);
            if (q.SessionId is null)
            {
                var target = Frontend(cfg);
                return SeeOther(ctx, q.Port is null ? target : $"{target}?port={q.Port}");
            }

            if (!IsValidSessionId(q.SessionId)) return Results.BadRequest("Invalid session ID");

            ctx.Response.Cookies.Append("session_id", q.SessionId,
                new CookieOptions { Path = "/", HttpOnly = true });

            // Launcher flow goes straight to Discord; the legacy flow detours via Crux.
            return SeeOther(ctx,
                q.Launcher ? "/auth/discord?launcher=true" : $"{Frontend(cfg)}/auth");
        });

        // Step 2: bounce to Discord. port/launcher ride along in `state`, the only
        // parameter that survives the OAuth redirect chain.
        app.MapGet("/auth/discord", (HttpContext ctx, IConfiguration cfg) =>
        {
            var q = LoginQuery.From(ctx.Request.Query);
            var parts = new List<string>();
            if (q.Port is not null) parts.Add(q.Port.Value.ToString());
            if (q.Launcher) parts.Add("launcher");
            var state = parts.Count > 0 ? string.Join(',', parts) : Guid.NewGuid().ToString("N");

            var url = Cfg(cfg, "AUTH_URL", "https://discord.com/api/oauth2/authorize?response_type=code")
                + $"&client_id={Uri.EscapeDataString(Required(cfg, "CLIENT_ID"))}"
                + $"&redirect_uri={Uri.EscapeDataString(RedirectUrl(cfg))}"
                + $"&scope=identify&state={Uri.EscapeDataString(state)}";

            return SeeOther(ctx, url);
        });

        // Step 3: Discord calls us back. Exchange -> identity -> whitelist -> account -> JWT.
        app.MapGet("/auth/authorized", async (
            HttpContext ctx, IConfiguration cfg, IHttpClientFactory http, IMemoryCache cache,
            AccountStore store, JwtService jwt, CancellationToken ct) =>
        {
            var code = ctx.Request.Query["code"].ToString();
            var state = ctx.Request.Query["state"].ToString();
            if (string.IsNullOrEmpty(code)) return Results.BadRequest("Missing code");

            var parts = state.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var isLauncher = parts.Contains("launcher");
            var statePort = parts.Select(p => ushort.TryParse(p, out var v) ? (ushort?)v : null)
                .FirstOrDefault(v => v is not null);

            var client = http.CreateClient();
            var accessToken = await ExchangeCodeAsync(client, cfg, code, ct);
            if (accessToken is null) return Results.BadRequest("Token exchange failed");

            var user = await FetchUserAsync(client, accessToken, ct);
            if (user?.id is null) return Results.BadRequest("Could not read Discord profile");

            if (!await IsWhitelistedAsync(client, cfg, user.id, ct))
            {
                return isLauncher
                    ? Results.Content(UnauthorizedPage, "text/html; charset=utf-8", null, 401)
                    : SeeOther(ctx, $"{Frontend(cfg)}/unauthorized");
            }

            var account = await store.GetOrCreateByDiscordIdAsync(user.id, ct);
            if (account is null)
                return Results.Conflict($"Username '{user.id}' is taken by a non-Discord account.");

            // `sub` is the account's Username, which every handler resolves against -
            // and for a Discord account that IS the snowflake, so /misc/avatar can
            // still call Discord with it directly. The display name rides in `name`.
            var token = jwt.MintProfile(account.Username, user.username ?? "", user.avatar ?? "");

            // Park the JWT where /auth/token/poll can collect it. Both flows use this:
            // the launcher reads it directly, the legacy flow gets it as a fallback.
            var sessionId = ctx.Request.Cookies["session_id"];
            if (sessionId is not null && IsValidSessionId(sessionId))
                cache.Set(HandoffKey(sessionId), token, HandoffTtl);

            if (isLauncher) return Results.Content(LoggedInPage, "text/html; charset=utf-8");

            var redirect = $"{Frontend(cfg)}/login?name={Uri.EscapeDataString(user.username ?? "")}"
                + $"&token={Uri.EscapeDataString(token)}"
                + $"&avatar={Uri.EscapeDataString(user.avatar ?? "default.png")}"
                + $"&userid={Uri.EscapeDataString(user.id)}";
            if (statePort is not null) redirect += $"&port={statePort}";
            return SeeOther(ctx, redirect);
        });

        // The Crux frontend hands the token back here; the client polls for it below.
        app.MapPost("/auth/token/submit", async (HttpContext ctx, IMemoryCache cache) =>
        {
            var sessionId = ctx.Request.Cookies["session_id"];
            if (sessionId is null || !IsValidSessionId(sessionId))
                return Results.BadRequest("Missing or invalid session_id cookie");

            var body = await ctx.Request.ReadFromJsonAsync<TokenPayload>();
            if (string.IsNullOrEmpty(body?.token)) return Results.BadRequest();

            cache.Set(HandoffKey(sessionId), body.token, HandoffTtl);
            return Results.Json(new { success = true, message = "Token stored successfully" });
        });

        app.MapPost("/auth/token/poll", async (HttpContext ctx, IMemoryCache cache) =>
        {
            var body = await ctx.Request.ReadFromJsonAsync<SessionIdRequest>();
            if (body?.session_id is null || !IsValidSessionId(body.session_id))
                return Results.BadRequest("Invalid session ID");

            // One-time use. Rust's comment claims this but it only ever reads, so a
            // parked token stays collectable for its whole TTL.
            //
            // NOT a fix for the underlying flaw: session_id is chosen by the client
            // and /auth/token/poll is unauthenticated, so anyone who knows one can
            // collect whatever JWT lands there. Closing that needs a launcher-side
            // proof-of-possession (send a hash at /auth/login, poll with the
            // preimage), which is a protocol change on both ends.
            var key = HandoffKey(body.session_id);
            if (!cache.TryGetValue<string>(key, out var token) || token is null)
                return Results.Json(new { error = "Session ID not found" }, statusCode: 404);

            cache.Remove(key);
            return Results.Json(new TokenPayload { token = token });
        });

        // Turnstile. Trades a valid login token for a 30-minute abuse_exemption cookie.
        app.MapPost("/auth/captcha", async (
            HttpContext ctx, IConfiguration cfg, IHttpClientFactory http, JwtService jwt,
            CancellationToken ct) =>
        {
            var body = await ctx.Request.ReadFromJsonAsync<CaptchaRequest>(ct);
            if (body?.token is null
                || !jwt.TryVerifyClaims(body.token, out var claims)
                || claims.Ephemeral
                || claims.Captcha)
            {
                return Results.Unauthorized();
            }
            var sub = claims.Sub;

            var secret = cfg["CAPTCHA_SECRET_KEY"];
            if (string.IsNullOrEmpty(secret))
                return Results.Json(new { error = "Missing CAPTCHA_SECRET_KEY" }, statusCode: 503);

            var res = await http.CreateClient().PostAsJsonAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify",
                new { secret, response = body.captchaToken }, ct);
            var verdict = await res.Content.ReadFromJsonAsync<CaptchaVerifyResponse>(ct);

            if (verdict?.success != true)
            {
                return Results.Json(new
                {
                    success = false,
                    error = "Captcha verification failed",
                    error_codes = string.Join(", ", verdict?.error_codes ?? ["unknown"]),
                }, statusCode: 400);
            }

            ctx.Response.Cookies.Append("abuse_exemption", jwt.MintCaptcha(sub),
                new CookieOptions { Path = "/", HttpOnly = true });
            return Results.Json(new { success = true, message = "Captcha verification successful" });
        });

        // Lives under /misc in Rust's router even though it is defined in auth.rs.
        app.MapPost("/misc/avatar", async (
            HttpContext ctx, IConfiguration cfg, IHttpClientFactory http, IMemoryCache cache,
            JwtService jwt, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            ctx.Response.Headers.Pragma = "no-cache";
            ctx.Response.Headers.Expires = "0";

            var body = await ctx.Request.ReadFromJsonAsync<TokenPayload>(ct);
            if (body?.token is null || !jwt.TryVerify(body.token, out var sub))
                return SeeOther(ctx, DefaultAvatar);

            if (cache.TryGetValue<string>("avatar:" + sub, out var cached) && cached is not null)
                return SeeOther(ctx, cached);

            var guildId = cfg["DISCORD_GUILD_ID"];
            var url = DefaultAvatar;
            if (!string.IsNullOrEmpty(guildId))
            {
                var member = await GetGuildMemberAsync(http.CreateClient(), cfg, guildId, sub, ct);
                if (member?.user?.avatar is { } file)
                    url = $"https://cdn.discordapp.com/avatars/{sub}/{file}?size=160";
            }

            cache.Set("avatar:" + sub, url, AvatarTtl);
            return SeeOther(ctx, url);
        });

        return app;
    }

    // --- Discord calls -------------------------------------------------------

    private static async Task<string?> ExchangeCodeAsync(
        HttpClient client, IConfiguration cfg, string code, CancellationToken ct)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = Required(cfg, "CLIENT_ID"),
            ["client_secret"] = Required(cfg, "CLIENT_SECRET"),
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUrl(cfg),
        });

        var res = await client.PostAsync(
            Cfg(cfg, "TOKEN_URL", "https://discord.com/api/oauth2/token"), form, ct);
        if (!res.IsSuccessStatusCode) return null;
        return (await res.Content.ReadFromJsonAsync<TokenResponse>(ct))?.access_token;
    }

    private static async Task<DiscordUser?> FetchUserAsync(
        HttpClient client, string accessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
        req.Headers.Authorization = new("Bearer", accessToken);
        var res = await client.SendAsync(req, ct);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<DiscordUser>(ct) : null;
    }

    private static async Task<GuildMember?> GetGuildMemberAsync(
        HttpClient client, IConfiguration cfg, string guildId, string memberId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://discord.com/api/v9/guilds/{guildId}/members/{memberId}");
        req.Headers.TryAddWithoutValidation("Authorization", $"Bot {cfg["DISCORD_TOKEN"]}");
        var res = await client.SendAsync(req, ct);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<GuildMember>(ct) : null;
    }

    /// Rust checks guild membership and short-circuits on six snowflakes baked into
    /// the binary. Here both lists come from config, and an unset DISCORD_GUILD_ID
    /// means "no whitelist" - a self-hosted server has nobody to gate.
    private static async Task<bool> IsWhitelistedAsync(
        HttpClient client, IConfiguration cfg, string memberId, CancellationToken ct)
    {
        var allowed = (cfg["DISCORD_WHITELIST_IDS"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (allowed.Contains(memberId)) return true;

        var guildId = cfg["DISCORD_GUILD_ID"];
        if (string.IsNullOrEmpty(guildId)) return true;

        using var req = new HttpRequestMessage(HttpMethod.Head,
            $"https://discord.com/api/v9/guilds/{guildId}/members/{memberId}");
        req.Headers.TryAddWithoutValidation("Authorization", $"Bot {cfg["DISCORD_TOKEN"]}");
        try { return (await client.SendAsync(req, ct)).IsSuccessStatusCode; }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return false; }
    }

    // --- helpers -------------------------------------------------------------

    /// 303, which every redirect in auth.rs uses (axum's Redirect::to IS See Other).
    /// Results.Redirect only speaks 301/302/307/308, and the difference matters: a
    /// POST to /misc/avatar must become a GET of the CDN URL.
    private static IResult SeeOther(HttpContext ctx, string location)
    {
        ctx.Response.Headers.Location = location;
        return Results.StatusCode(StatusCodes.Status303SeeOther);
    }

    private static string HandoffKey(string sessionId) => "auth-handoff:" + sessionId;

    /// Rust validate_session_id: ASCII, 20..=60 chars.
    internal static bool IsValidSessionId(string id) =>
        id.Length is >= 20 and <= 60 && id.All(char.IsAscii);

    private static string Cfg(IConfiguration cfg, string key, string fallback) =>
        string.IsNullOrEmpty(cfg[key]) ? fallback : cfg[key]!;

    private static string Required(IConfiguration cfg, string key) =>
        cfg[key] ?? throw new InvalidOperationException($"Missing {key}!");

    private static string RedirectUrl(IConfiguration cfg) =>
        Cfg(cfg, "REDIRECT_URL", "http://localhost:8080/auth/authorized");

    private static string Frontend(IConfiguration cfg) =>
        Required(cfg, "FRONTEND_URL").TrimEnd('/');

    private readonly record struct LoginQuery(string? SessionId, ushort? Port, bool Launcher)
    {
        public static LoginQuery From(IQueryCollection q) => new(
            q["session_id"].ToString() is { Length: > 0 } s ? s : null,
            ushort.TryParse(q["port"], out var p) ? p : null,
            q["launcher"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase));
    }

    // --- wire shapes ---------------------------------------------------------

    public sealed class TokenPayload { public string token { get; set; } = ""; }
    public sealed class SessionIdRequest { public string? session_id { get; set; } }

    public sealed class CaptchaRequest
    {
        public string? token { get; set; }
        public string? captchaToken { get; set; }
    }

    private sealed class CaptchaVerifyResponse
    {
        public bool success { get; set; }
        [JsonPropertyName("error-codes")] public List<string>? error_codes { get; set; }
    }

    private sealed class TokenResponse { public string? access_token { get; set; } }

    private sealed class DiscordUser
    {
        public string? id { get; set; }
        public string? avatar { get; set; }
        public string? username { get; set; }
    }

    private sealed class GuildMember { public DiscordUser? user { get; set; } }

    // --- launcher landing pages (verbatim from auth.rs) ----------------------

    private const string PageStyle =
        "body { background: #1a1a2e; color: #e0e0e0; font-family: system-ui; display: flex;"
        + " align-items: center; justify-content: center; height: 100vh; margin: 0; }"
        + " .card { text-align: center; padding: 2rem; }";

    private const string UnauthorizedPage =
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Lethe — Unauthorized</title>"
        + "<style>" + PageStyle + " h1 { color: #e74c3c; }</style></head>"
        + "<body><div class=\"card\"><h1>&#10007; Unauthorized</h1>"
        + "<p>You are not whitelisted. Join our Discord server to request access.</p></div></body></html>";

    private const string LoggedInPage =
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Lethe — Logged In</title>"
        + "<style>" + PageStyle + " h1 { color: #7c8cf8; }</style></head>"
        + "<body><div class=\"card\"><h1>&#10003; Logged in!</h1>"
        + "<p>You can close this window and return to the launcher.</p></div></body></html>";
}
