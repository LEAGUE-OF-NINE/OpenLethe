using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;

namespace OpenLethe.Server.Handlers;

/// Shared boilerplate for stateful handlers: resolve the authed account, read the
/// request parameters, and persist. Mirrors the pattern in LoadUserDataAll.
internal static class HandlerContext
{
    /// The account for the middleware-attached subject, or null (caller 401s).
    public static async Task<Account?> ResolveAsync(HttpContext ctx)
    {
        if (ctx.Items["sub"] is not string sub) return null;
        return await ctx.RequestServices.GetRequiredService<AccountStore>().FindByUsernameAsync(sub);
    }

    /// Deserialize the envelope and return its `parameters`.
    public static async Task<TReq?> ReadParamsAsync<TReq>(HttpContext ctx)
    {
        var env = await JsonSerializer.DeserializeAsync<global::RequestPacket<TReq>>(
            ctx.Request.Body, global::PacketJson.Options);
        return env is null ? default : env.parameters;
    }

    /// Persist mutations made to the tracked account (same request scope as AccountStore).
    public static Task SaveAsync(HttpContext ctx) =>
        ctx.RequestServices.GetRequiredService<AppDbContext>().SaveChangesAsync();
}
