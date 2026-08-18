using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace OpenLethe.Server.Auth;

/// Ported from lethe-server middleware verify_jwt_middleware. Signature-only:
/// verifies userAuth.authCode and attaches the subject. No DB access.
public sealed class JwtAuthMiddleware(RequestDelegate next, JwtService jwt)
{
    public async Task Invoke(HttpContext ctx)
    {
        if (IsExempt(ctx.Request.Path))
        {
            await next(ctx);
            return;
        }

        ctx.Request.EnableBuffering(); // so MapPacket can re-read the body afterward
        string? authCode;
        try
        {
            var env = await JsonSerializer.DeserializeAsync<global::RequestPacket<JsonElement>>(
                ctx.Request.Body, global::PacketJson.Options);
            authCode = env?.userAuth?.auth_code;
        }
        catch
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        finally
        {
            ctx.Request.Body.Position = 0;
        }

        // Ephemeral (dashboard) and captcha (abuse_exemption cookie) tokens are
        // narrower credentials that happen to be signed with the same key. Without
        // this they would work as full game auth codes, so a frontend leaking either
        // one would leak account access.
        if (authCode is null
            || !jwt.TryVerifyClaims(authCode, out var claims)
            || claims.Ephemeral
            || claims.Captcha)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        ctx.Items["sub"] = claims.Sub;
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
