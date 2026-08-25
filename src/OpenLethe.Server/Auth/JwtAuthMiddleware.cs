using System.Text.Json;

namespace OpenLethe.Server.Auth;

/// Ported from lethe-server middleware verify_jwt_middleware. Signature-only:
/// verifies userAuth.authCode and attaches the subject. No DB access.
public sealed class JwtAuthMiddleware(RequestDelegate next, JwtService jwt)
{
    public async Task Invoke(HttpContext ctx)
    {
        // Nothing matched this path, so there is no envelope to demand: let routing
        // answer 404. Without this the middleware rejects every unrouted request as a
        // malformed packet, which is how a plain GET / became a confusing 400 instead
        // of "no such route" (Rust layers this only over the game router, so an
        // unmatched path 404s there too).
        if (ctx.GetEndpoint() is null || IsExempt(ctx.Request.Path))
        {
            await next(ctx);
            return;
        }

        string? authCode;
        JsonElement parameters;
        try
        {
            var env = await JsonSerializer.DeserializeAsync<global::RequestPacket<JsonElement>>(
                ctx.Request.Body, global::PacketJson.Options);
            authCode = env?.userAuth?.auth_code;
            parameters = env?.parameters ?? default;
        }
        catch
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // The captcha token backs the abuse_exemption cookie and is a different
        // credential class - it must never be a game auth code.
        //
        // The EPHEMERAL token deliberately is one: Crux mints it via
        // /dashboard/auth/token and hands it to the game precisely so the long-lived
        // browser token never reaches the game process. Its short life IS the
        // mitigation, and /dashboard/auth/token refuses to mint another from it.
        // Rejecting it here broke login for every real client.
        if (authCode is null
            || !jwt.TryVerifyClaims(authCode, out var claims)
            || claims.Captcha)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        ctx.Items["sub"] = claims.Sub;

        // Hand the parsed `parameters` on rather than rewinding the body for the handler
        // to parse again, so every protected request parses once. Stashed only after the
        // credential check, so a rejected request drops the document straight away rather
        // than holding it for the life of the response. The JsonElement is backed by its
        // own document, not by the request stream, so it stays valid from here on.
        ctx.Items[Handlers.HandlerContext.ParamsItemKey] = parameters;
        await next(ctx);
    }

    // Rust keys off the first path segment != "login". We also exempt the new
    // /auth/* login surface and /health. /dashboard and /serverinfos are exempt
    // because Rust hangs them off the outer router with no verify_jwt layer -
    // dashboard requests carry the token in the body, not in a userAuth envelope,
    // so each dashboard handler authenticates itself.
    private static bool IsExempt(PathString path)
    {
        var s = path.Value ?? "";
        return s.StartsWith("/login/", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("/dashboard/", StringComparison.OrdinalIgnoreCase)
            // /misc carries its token in the body or the abuse_exemption cookie,
            // never in a userAuth envelope; Rust hangs it off the outer router too.
            || s.StartsWith("/misc/", StringComparison.OrdinalIgnoreCase)
            || s.Equals("/serverinfos", StringComparison.OrdinalIgnoreCase)
            // Launcher downloads these before login, so they cannot require a token.
            || s.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || s.Equals("/limbus-manifest.txt", StringComparison.OrdinalIgnoreCase)
            || s.Equals("/noticeMeta.json", StringComparison.OrdinalIgnoreCase)
            || s.Equals("/health", StringComparison.OrdinalIgnoreCase);
    }
}

public static class JwtAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseJwtAuth(this IApplicationBuilder app) =>
        app.UseMiddleware<JwtAuthMiddleware>();
}
