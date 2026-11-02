using System.Text.Json.Nodes;
using OpenLethe.Data;
using OpenLethe.Server.MirrorDungeon.Data;
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
        Upload<ThemeStatic>(app, "/custom/upload/mirrordungeon-theme-floor", SaveColumn.CustomTheme,
            (a, v) => a.CustomTheme = v, "Successfully updated theme floors");

        // Rust answers "Successfully updated theme floors" from this handler too
        // (copy-paste in custom/mod.rs) - kept verbatim.
        Upload<CustomIdentity>(app, "/custom/upload/personality", SaveColumn.CustomIdentities,
            (a, v) => a.CustomIdentities = v, "Successfully updated theme floors");

        // The mod's custom_limbus_data/ego-gift-mirrordungeon and mirrordungeon-egogift-droppool.
        // Only the fields the server reads survive (MdEgo, DungeonEgoGiftDropPool); everything else
        // about a gift lives in the client. MdCatalog lays both over the static tables.
        Upload<MdEgo>(app, "/custom/upload/ego-gift-mirrordungeon", SaveColumn.CustomEgoGifts,
            (a, v) => a.CustomEgoGifts = v, "Successfully updated ego gifts");
        Upload<DungeonEgoGiftDropPool>(app, "/custom/upload/mirrordungeon-egogift-droppool",
            SaveColumn.CustomEgoGiftDropPools, (a, v) => a.CustomEgoGiftDropPools = v,
            "Successfully updated ego gift drop pools");

        // custom_limbus_data/battle-mirrordungeon: only id, rewardList and participantInfo are kept -
        // what a stage hands out on a win and how many identities it deploys.
        Upload<AbStage>(app, "/custom/upload/battle-mirrordungeon", SaveColumn.CustomMdBattles,
            (a, v) => a.CustomMdBattles = v, "Successfully updated mirror dungeon battles");

        // custom_limbus_data/mirror-dungeon-common-data. Unlike the kinds above, each file IS one
        // object (the table is unlisted), so the list holds the files themselves. Kept whole -
        // MdCommonSource lays each over the season's common data key by key. A file a section of
        // which has the wrong shape is skipped (and named in the reply) rather than failing the
        // upload: the client mod fires these at startup and shows any error as a popup.
        app.MapPost("/custom/upload/mirror-dungeon-common-data", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.CustomMdCommonData);
            if (account is null) return Results.Unauthorized();

            var p = await HandlerContext.ReadParamsAsync<ValList<JsonObject?>>(ctx);
            if (p is null) return Results.BadRequest();

            var kept = new List<JsonObject>();
            var skipped = new List<string>();
            foreach (var (file, i) in (p.list ?? new()).Select((f, i) => (f, i)))
            {
                if (file is null) continue;
                if (MdCommonSource.Validate(file) is { } error) skipped.Add($"file {i}: {error}");
                else kept.Add(file);
            }

            account.CustomMdCommonData = AccountFields.Set(kept);
            await HandlerContext.SaveAsync(ctx);

            var message = skipped.Count == 0
                ? "Successfully updated mirror dungeon common data"
                : "Updated mirror dungeon common data; skipped " + string.Join("; ", skipped);
            return Results.Json(new { message }, global::PacketJson.Options);
        });

        // The client mod posts about a dozen more kinds at startup (skin, lobby-bgm,
        // battle-effect, error-code-table, mirrordungeon-constraint, ...). Rust has a route for
        // each; this port models only the ones above, and the rest used to hit the framework's
        // empty-body 404, which Lethe's HTTP patch throws on (ArgumentOutOfRangeException in
        // Patches.Request.PostWebRequest) and the game shows as an error. Acknowledge every other
        // kind with the same bare {"message": ...}; nothing reads them, so nothing is stored.
        // Literal routes above win over this parameterized one.
        app.MapPost("/custom/upload/{kind}", async (HttpContext ctx, string kind) =>
        {
            if (!await HandlerContext.ExistsAsync(ctx)) return Results.Unauthorized();
            return Results.Json(new { message = $"Successfully updated {kind}" }, global::PacketJson.Options);
        });

        return app;
    }

    /// parameters is a list OF lists (one inner list per uploaded file); Rust
    /// concatenates them into one flat column value, replacing what was there.
    private static void Upload<T>(
        IEndpointRouteBuilder app, string path, SaveColumn column, Action<Account, string> write, string message) =>
        app.MapPost(path, async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, column);
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
