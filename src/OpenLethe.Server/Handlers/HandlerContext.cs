using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;
using OpenLethe.Server.MirrorDungeon.Data;

namespace OpenLethe.Server.Handlers;

/// The jsonb documents a scoped handler reads and writes. See ResolveAsync(ctx, columns).
internal enum SaveColumn
{
    Md, StoryMd, Railway, Story, Chapter, Chapter10Rpg,
    BossRaid, Formations, Announcers, UserInfo, Egos, Personalities, CustomIdentities, CustomTheme,
    CustomEgoGifts, CustomEgoGiftDropPools, CustomMdBattles,
}

/// Shared boilerplate for stateful handlers: resolve the authed account, read the
/// request parameters, and persist. Mirrors the pattern in LoadUserDataAll.
internal static class HandlerContext
{
    /// Where JwtAuthMiddleware leaves the envelope's already-parsed `parameters`.
    internal const string ParamsItemKey = "packet.parameters";

    private const string ScopeItemKey = "account.scope";

    /// Narrow SELECTs. `accounts` carries fifteen jsonb documents; most handlers read one
    /// or two, so the unscoped ResolveAsync below makes Postgres detoast and ship the rest
    /// on every packet - and a burst of those detoasts is what runs the DB VM out of memory.
    private static readonly Dictionary<SaveColumn, string> ColumnNames = new()
    {
        [SaveColumn.Md] = nameof(Account.MdSaveInfo),
        [SaveColumn.StoryMd] = nameof(Account.StoryMdSaveInfo),
        [SaveColumn.Railway] = nameof(Account.RailwaySaveInfo),
        [SaveColumn.Story] = nameof(Account.StorySaveInfo),
        [SaveColumn.Chapter] = nameof(Account.ChapterState),
        [SaveColumn.Chapter10Rpg] = nameof(Account.Chapter10RpgSaveInfo),
        [SaveColumn.BossRaid] = nameof(Account.BossRaidSaveInfo),
        [SaveColumn.Formations] = nameof(Account.Formations),
        [SaveColumn.Announcers] = nameof(Account.Announcers),
        [SaveColumn.UserInfo] = nameof(Account.UserInfo),
        [SaveColumn.Egos] = nameof(Account.Egos),
        [SaveColumn.Personalities] = nameof(Account.Personalities),
        [SaveColumn.CustomIdentities] = nameof(Account.CustomIdentities),
        [SaveColumn.CustomTheme] = nameof(Account.CustomTheme),
        [SaveColumn.CustomEgoGifts] = nameof(Account.CustomEgoGifts),
        [SaveColumn.CustomEgoGiftDropPools] = nameof(Account.CustomEgoGiftDropPools),
        [SaveColumn.CustomMdBattles] = nameof(Account.CustomMdBattles),
    };

    /// `a => new Account { Id = a.Id, <col> = a.<col>, ... }`, which EF turns into
    /// `SELECT "Id", "<col>", ...`. Built once per distinct column set.
    private static readonly ConcurrentDictionary<string, Expression<Func<Account, Account>>> Projections = new();

    private static Expression<Func<Account, Account>> Projection(string[] names) =>
        Projections.GetOrAdd(string.Join(',', names), _ =>
        {
            var a = Expression.Parameter(typeof(Account), "a");
            var binds = names.Prepend(nameof(Account.Id)).Select(n =>
            {
                var p = typeof(Account).GetProperty(n)!;
                return (MemberBinding)Expression.Bind(p, Expression.Property(a, p));
            });
            return Expression.Lambda<Func<Account, Account>>(
                Expression.MemberInit(Expression.New(typeof(Account)), binds), a);
        });

    /// The uploads MdCatalog is built from. See the Md branch in ResolveAsync.
    private static readonly string[] CatalogColumns =
    {
        nameof(Account.CustomTheme), nameof(Account.CustomEgoGifts), nameof(Account.CustomEgoGiftDropPools),
        nameof(Account.CustomMdBattles),
    };

    /// Account's writable string columns. See the blanking loop in ResolveAsync.
    private static readonly PropertyInfo[] StringColumns = typeof(Account)
        .GetProperties().Where(p => p.PropertyType == typeof(string) && p.CanWrite).ToArray();

    /// The account for the middleware-attached subject, or null (caller 401s).
    public static async Task<Account?> ResolveAsync(HttpContext ctx)
    {
        if (ctx.Items["sub"] is not string sub) return null;
        return await ctx.RequestServices.GetRequiredService<AccountStore>().FindByUsernameAsync(sub);
    }

    /// True when the middleware-attached subject has an account. For routes that only
    /// authenticate and never read the row.
    public static Task<bool> ExistsAsync(HttpContext ctx) =>
        ctx.Items["sub"] is string sub
            ? ctx.RequestServices.GetRequiredService<AppDbContext>().Accounts.AnyAsync(a => a.Username == sub)
            : Task.FromResult(false);

    /// Same, but reads ONLY Id + the named columns. Every other property on the returned
    /// Account reads as null, NOT as the stored value, and SaveAsync throws on a write to
    /// one - so name every column the handler (and the helpers it calls) touches.
    /// AccountDefaults.DerivePersonalities reads Personalities and CustomIdentities.
    ///
    /// Resolve ONCE per request. The stub this tracks stands in for the whole row, so a
    /// second resolve in the same request - scoped or via AccountStore - gets the stub
    /// back from EF's identity resolution rather than a fresh row, and every unloaded
    /// column reads as null.
    public static async Task<Account?> ResolveAsync(HttpContext ctx, params SaveColumn[] columns)
    {
        if (ctx.Items["sub"] is not string sub) return null;

        var names = columns.Select(c => ColumnNames[c]).Distinct().ToArray();

        // A Mirror Dungeon request also reads the account's uploaded packs, gifts, drop pools and
        // battles in the same SELECT and makes them the request's MdCatalog, so every rule below sees them. They
        // stay out of `names`: the blanking loop clears them again and the handler can't write them.
        var loadCatalog = columns.Contains(SaveColumn.Md) || columns.Contains(SaveColumn.StoryMd);
        var selected = loadCatalog ? names.Union(CatalogColumns).ToArray() : names;

        var db = ctx.RequestServices.GetRequiredService<AppDbContext>();
        var account = await db.Accounts
            .AsNoTracking()
            .Where(a => a.Username == sub)
            .Select(Projection(selected))
            .SingleOrDefaultAsync();
        if (account is null) return null;

        if (loadCatalog)
            MdCatalog.SetCurrent(MdCatalog.FromColumns(
                account.CustomTheme, account.CustomEgoGifts, account.CustomEgoGiftDropPools,
                account.CustomMdBattles));

        // Blank the columns the projection didn't load, BEFORE Attach snapshots them.
        // They arrive holding Account's field initializers ("{}", "[]"), so a handler
        // assigning that same placeholder - `account.StorySaveInfo = "{}"` is this
        // codebase's idiom for clearing a save - would leave IsModified false, and the
        // guard in SaveAsync would wave through a write that EF then silently drops.
        // Against a null snapshot any assignment at all registers.
        foreach (var p in StringColumns) if (!names.Contains(p.Name)) p.SetValue(account, null);

        // Attach so change tracking still drives the write: the scoped columns carry
        // real original values, so EF marks one modified iff the handler actually changed
        // it and the UPDATE names only those.
        db.Attach(account);
        ctx.Items[ScopeItemKey] = names;
        return account;
    }

    /// Deserialize the envelope and return its `parameters`.
    public static async Task<TReq?> ReadParamsAsync<TReq>(HttpContext ctx)
    {
        // JwtAuthMiddleware already parsed this body to read the auth code and left the
        // `parameters` subtree behind; re-reading the stream here would parse the same
        // bytes a second time. Routes it exempts (/login, /auth, /dashboard, /misc) leave
        // no stash, so those still parse for themselves. Undefined = no `parameters` key,
        // which the handlers answer with a 400 of their own.
        if (ctx.Items[ParamsItemKey] is JsonElement stashed)
            return stashed.ValueKind == JsonValueKind.Undefined ? default : stashed.Deserialize<TReq>(global::PacketJson.Options);

        var env = await JsonSerializer.DeserializeAsync<global::RequestPacket<TReq>>(
            ctx.Request.Body, global::PacketJson.Options);
        return env is null ? default : env.parameters;
    }

    /// Persist mutations made to the tracked account (same request scope as AccountStore).
    public static Task SaveAsync(HttpContext ctx)
    {
        var db = ctx.RequestServices.GetRequiredService<AppDbContext>();

        // A scoped account never loaded the other columns, so writing one would persist
        // nothing while looking like it worked. Fail loudly instead. This catches writes
        // only - a scoped handler READING an unloaded column just sees null, which no
        // guard here can see. (Entries<T>() runs DetectChanges for us.)
        if (ctx.Items[ScopeItemKey] is string[] scoped
            && db.ChangeTracker.Entries<Account>().SelectMany(e => e.Properties)
                .FirstOrDefault(p => p.IsModified && !scoped.Contains(p.Metadata.Name))?.Metadata.Name is string stray)
        {
            throw new InvalidOperationException(
                $"Handler wrote Account.{stray} but resolved the account scoped to {string.Join(", ", scoped)}; " +
                "that column was never loaded, so the write would be dropped - add it to the scope.");
        }

        return db.SaveChangesAsync();
    }
}
