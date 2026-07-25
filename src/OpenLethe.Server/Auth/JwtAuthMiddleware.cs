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

        if (authCode is null || !jwt.TryVerify(authCode, out var sub))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        ctx.Items["sub"] = sub;
        await next(ctx);
    }

    // Rust keys off the first path segment != "login". We also exempt the new
    // /auth/* login surface and /health.
    private static bool IsExempt(PathString path)
    {
        var s = path.Value ?? "";
        return s.StartsWith("/login/", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)
            || s.Equals("/health", StringComparison.OrdinalIgnoreCase);
    }
}

public static class JwtAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseJwtAuth(this IApplicationBuilder app) =>
        app.UseMiddleware<JwtAuthMiddleware>();
}
