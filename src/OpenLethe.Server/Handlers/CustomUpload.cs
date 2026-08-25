using OpenLethe.Data;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Port of lethe-server/server/src/custom/mod.rs - the mod-side upload routes.
/// The client mod posts its custom MD theme floors and custom identities here and
/// the server stores them on the account. Authenticated by the userAuth envelope
/// like the game routes (Rust layers verify_jwt over custom_router), but the reply
/// is a bare {"message": ...}, not a ResponsePacket.
public static class CustomUploadEndpoints
{
    public static IEndpointRouteBuilder MapCustomUpload(this IEndpointRouteBuilder app)
    {
        Upload<ThemeStatic>(app, "/custom/upload/mirrordungeon-theme-floor",
            (a, v) => a.CustomTheme = v, "Successfully updated theme floors");

        // Rust answers "Successfully updated theme floors" from this handler too
        // (copy-paste in custom/mod.rs) - kept verbatim.
        Upload<CustomIdentity>(app, "/custom/upload/personality",
            (a, v) => a.CustomIdentities = v, "Successfully updated theme floors");

        return app;
    }

    /// parameters is a list OF lists (one inner list per uploaded file); Rust
    /// concatenates them into one flat column value, replacing what was there.
    private static void Upload<T>(
        IEndpointRouteBuilder app, string path, Action<Account, string> write, string message) =>
        app.MapPost(path, async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();

            var p = await HandlerContext.ReadParamsAsync<ValList<ValList<T>>>(ctx);
            if (p is null) return Results.BadRequest();

            var flat = (p.list ?? new()).SelectMany(inner => inner.list ?? new()).ToList();
            write(account, AccountFields.Set(flat));
            await HandlerContext.SaveAsync(ctx);

            return Results.Json(new { message }, global::PacketJson.Options);
        });

    /// Rust models/src/resources.rs ValList.
    private sealed class ValList<T>
    {
        public List<T>? list { get; set; }
    }
}
