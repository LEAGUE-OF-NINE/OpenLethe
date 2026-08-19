using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Resources;
using OpenLethe.Server.Auth;
using OpenLethe.Server.Defaults;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Port of lethe-server/server/src/dashboard.rs - out-of-band account editing for a
/// web frontend. Unlike every game route these carry the token in the request BODY
/// (no userAuth envelope), which is why JwtAuthMiddleware exempts /dashboard and each
/// handler authenticates itself.
public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboard(this IEndpointRouteBuilder app)
    {
        // Thirteen of the routes are get/set on one opaque Account column. They differ
        // only in the field name the payload travels under and whether the response
        // wraps it - so they share one generic mapper.
        // Both derive: Rust reads these columns raw but seeds them with the shipped
        // defaults when the account is created, so a raw read there is never empty.
        MapColumn<List<Ego>>(app, "/dashboard/egos", "/dashboard/egos/update", "list", wrap: true,
            a => a.Egos, (a, v) => a.Egos = v,
            merge: (cur, inc) => AccountFields.MergeById(cur, inc, e => e.ego_id),
            derive: AccountDefaults.DeriveEgos);

        MapColumn<List<ResultPersonality>>(app, "/dashboard/personalities", "/dashboard/personalities/update", "list", wrap: true,
            a => a.Personalities, (a, v) => a.Personalities = v,
            merge: (cur, inc) => AccountFields.MergeById(cur, inc, p => p.personality_id),
            derive: AccountDefaults.DerivePersonalities);

        MapColumn<UserInfo>(app, "/dashboard/userinfo", "/dashboard/userinfo/update", "userInfo", wrap: false,
            a => a.UserInfo, (a, v) => a.UserInfo = v);

        MapColumn<MirrorOriginSaveInfo>(app, "/dashboard/md/get", "/dashboard/md/update", "saveInfo", wrap: false,
            a => a.MdSaveInfo, (a, v) => a.MdSaveInfo = v);

        MapColumn<StorySaveInfo>(app, "/dashboard/storydungeon/get", "/dashboard/storydungeon/update", "data", wrap: false,
            a => a.StorySaveInfo, (a, v) => a.StorySaveInfo = v);

        MapColumn<StoryMirrorSaveInfo>(app, "/dashboard/storymirrordungeon/get", "/dashboard/storymirrordungeon/update", "data", wrap: false,
            a => a.StoryMdSaveInfo, (a, v) => a.StoryMdSaveInfo = v);

        // IngameId is an int identity column, not a JSON document.
        app.MapPost("/dashboard/ingameid", Route((account, _, _) =>
            Task.FromResult(Json(new IngameIdResponse { ingameId = account.IngameId }))));

        app.MapPost("/dashboard/ingameid/update", Route(async (account, body, ctx) =>
        {
            if (!body.TryGetProperty("ingameId", out var id) || !id.TryGetInt32(out var value))
                return Results.BadRequest();

            var db = ctx.RequestServices.GetRequiredService<AppDbContext>();
            if (value != account.IngameId
                && await db.Accounts.AnyAsync(a => a.IngameId == value && a.Id != account.Id))
            {
                return Results.Conflict($"ingameId {value} is already taken.");
            }

            account.IngameId = value;
            await SaveAsync(ctx);
            return Json(new IngameIdResponse { ingameId = value });
        }));

        app.MapPost("/dashboard/md/reset", Route(async (account, _, ctx) =>
        {
            account.MdSaveInfo = AccountFields.Set(new MirrorOriginSaveInfo
            {
                dungeonId = -1,
                idx = -1,
                currentInfo = new CurrentInfo { eid = -1 },
            });
            account.StorySaveInfo = AccountFields.Set(new StorySaveInfo());
            account.StoryMdSaveInfo = AccountFields.Set(new StoryMirrorSaveInfo { dungeonid = -1 });
            // Rust writes a sentinel RailwaySaveInfo row plus empty node data. OpenLethe
            // keeps railway state as one dungeonId -> run map, so "no runs" is the empty map.
            account.RailwaySaveInfo = "{}";
            await SaveAsync(ctx);
            return Json("Dungeon save info updated");
        }));

        // Rust reports "Chapter state reset successfully" from both handlers - kept verbatim.
        app.MapPost("/dashboard/chapterstate/reset", Route(SetChapterState(DefaultData.InitMainChapterState)));
        app.MapPost("/dashboard/chapterstate/complete", Route(SetChapterState(DefaultData.LoadMainChapterState)));

        app.MapPost("/dashboard/personalities/localize", Route((account, _, _) =>
        {
            // SortedDictionary matches Rust's BTreeMap: custom entries override the
            // shipped ones by id, and the response comes out id-ordered.
            var byId = new SortedDictionary<long, IdentityLocalizeFormat>();

            foreach (var format in StaticData.GetLocalizeList<IdentityLocalizeFormat>("en/EN_Personalities.json"))
            {
                format.sinner_id = (format.id / 100) % 100;
                byId[format.id] = format;
            }

            foreach (var custom in AccountFields.Get<List<CustomIdentity>>(account.CustomIdentities) ?? new())
            {
                byId[custom.id] = new IdentityLocalizeFormat
                {
                    id = custom.id,
                    title = "",
                    name = custom.characterId >= 1 && custom.characterId <= Sinners.Length
                        ? Sinners[custom.characterId - 1]
                        : "",
                    sinner_id = custom.characterId,
                    custom = true,
                };
            }

            return Task.FromResult(Json(byId.Values.ToList()));
        }));

        app.MapPost("/dashboard/auth/token", (Delegate)(async (HttpContext ctx) =>
        {
            var body = await ReadBodyAsync(ctx);
            if (body is null) return Results.BadRequest();
            if (!body.Value.TryGetProperty("token", out var t) || t.ValueKind != JsonValueKind.String)
                return Results.Unauthorized();

            var jwt = ctx.RequestServices.GetRequiredService<JwtService>();
            // An ephemeral token may not mint another one (Rust: same guard). No
            // whitelist check - OpenLethe logins are username trust-on-first-use.
            if (!jwt.TryVerify(t.GetString()!, out var sub, out var ephemeral) || ephemeral)
                return Results.Unauthorized();

            return Json(new TokenResponse { token = jwt.MintEphemeral(sub) });
        }));

        // Declared in dashboard.rs but served off the root router, unauthenticated.
        app.MapGet("/serverinfos", () => Results.Content(ServerInfosJson, "application/json"));

        return app;
    }

    private static void MapColumn<T>(
        IEndpointRouteBuilder app,
        string getPath,
        string updatePath,
        string field,
        bool wrap,
        Func<Account, string> read,
        Action<Account, string> write,
        Func<T, T, T>? merge = null,
        Func<Account, T>? derive = null) where T : new()
    {
        // `derive` supplies the shipped defaults for columns OpenLethe fills lazily
        // (personalities are never written by the game at all) - without it these
        // routes answer with an empty list for a fresh account.
        T Current(Account a) => derive is not null ? derive(a) : AccountFields.Get<T>(read(a)) ?? new T();

        app.MapPost(getPath, Route((account, _, _) =>
            Task.FromResult(Json(Shape(field, wrap, Current(account))))));

        app.MapPost(updatePath, Route(async (account, body, ctx) =>
        {
            if (!body.TryGetProperty(field, out var payload)) return Results.BadRequest();

            T value;
            try { value = payload.Deserialize<T>(global::PacketJson.Options) ?? new T(); }
            catch (JsonException) { return Results.BadRequest(); }

            // egos/personalities upsert by id (Rust update_egos/update_personalities);
            // every other column is replaced wholesale.
            if (merge is not null) value = merge(Current(account), value);

            write(account, AccountFields.Set(value));
            await SaveAsync(ctx);
            return Json(Shape(field, wrap, value));
        }));
    }

    private static Func<Account, JsonElement, HttpContext, Task<IResult>> SetChapterState(
        Func<List<MainChapterState>> build) =>
        async (account, _, ctx) =>
        {
            account.ChapterState = AccountFields.Set(build());
            await SaveAsync(ctx);
            return Json("Chapter state reset successfully");
        };

    /// Wraps a dashboard handler as a route delegate. The `Delegate` return type is
    /// load-bearing: a lambda typed Func&lt;HttpContext, Task&lt;IResult&gt;&gt; binds as a
    /// RequestDelegate and silently discards the result, sending an empty 200 (ASP0016).
    /// Reads the body once, authenticates the `token` it carries, and hands the account
    /// plus the body to the handler. 400 on an unparseable body, 401 on a bad token.
    private static Delegate Route(Func<Account, JsonElement, HttpContext, Task<IResult>> handler) =>
        async (HttpContext ctx) =>
        {
            var body = await ReadBodyAsync(ctx);
            if (body is null) return Results.BadRequest();

            if (!body.Value.TryGetProperty("token", out var t) || t.ValueKind != JsonValueKind.String)
                return Results.Unauthorized();
            if (!ctx.RequestServices.GetRequiredService<JwtService>()
                    .TryVerifyClaims(t.GetString()!, out var claims)
                || claims.Captcha)
            {
                return Results.Unauthorized();
            }

            var account = await ctx.RequestServices.GetRequiredService<AccountStore>()
                .FindByUsernameAsync(claims.Sub);
            if (account is null) return Results.Unauthorized();

            return await handler(account, body.Value, ctx);
        };

    private static async Task<JsonElement?> ReadBodyAsync(HttpContext ctx)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            return doc.RootElement.Clone();
        }
        catch (JsonException) { return null; }
    }

    private static Task SaveAsync(HttpContext ctx) =>
        ctx.RequestServices.GetRequiredService<AppDbContext>().SaveChangesAsync();

    private static object Shape<T>(string field, bool wrap, T value) =>
        wrap ? new Dictionary<string, object?> { [field] = value } : value!;

    private static IResult Json(object value) => Results.Json(value, global::PacketJson.Options);

    private static readonly string[] Sinners =
    [
        "Yi Sang", "Faust", "Don Quixote", "Ryōshū", "Meursault", "Hong Lu",
        "Heathcliff", "Ishmael", "Rodion", "Sinclair", "Outis", "Gregor",
    ];

    private const string ServerInfosJson = """
        [
          {
            "platform": "windows",
            "serviceType": "product",
            "serverId": "win_product",
            "versions": ["1.103.0","1.100.0","1.90.0","1.98.0","1.93.0","1.94.0","1.95.1","1.96.0","1.98.1","1.99.0"],
            "serverUrl": "https://www.limbuscompanyapi.com",
            "logServerUrl": "https://battlelog.limbuscompanyapi.com",
            "subAccountLogServerUrl": "https://subbattlelog.limbuscompanyapi.com",
            "noticeUrl": "https://notice.limbuscompanyapi.com",
            "cdnUrl": "",
            "enablePacketCrypt": true,
            "enablePacketOption": true,
            "enableBattleLogPacketCrypt": true,
            "enableBattleLogPacketOption": true,
            "enableSubAccountLogPacketCrypt": true,
            "enableSubAccountLogPacketOption": true,
            "enableAgreementVersionCheck": true,
            "enableNetworkingUIProcessPurchase": true,
            "enableCheckUpdateCatalogSteamFixed": true,
            "enableCheckUpdateCatalogToTitle": true
          }
        ]
        """;

    private sealed class IngameIdResponse { public int ingameId; }

    private sealed class TokenResponse { public string token = ""; }

    /// Rust dashboard.rs IdentityLocalizeFormat. No serde rename here, so `sinner_id`
    /// stays snake_case on the wire.
    private sealed class IdentityLocalizeFormat
    {
        public long id;
        public string title = "";
        public string name = "";
        public long sinner_id;
        public bool custom;
    }
}
