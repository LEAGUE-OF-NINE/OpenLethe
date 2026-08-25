using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;

namespace OpenLethe.Server.Handlers;

/// The one jsonb document a scoped handler reads and writes. See ResolveAsync(ctx, column).
internal enum SaveColumn { Md, StoryMd, Railway, Story, Chapter }

/// Shared boilerplate for stateful handlers: resolve the authed account, read the
/// request parameters, and persist. Mirrors the pattern in LoadUserDataAll.
internal static class HandlerContext
{
    /// Where JwtAuthMiddleware leaves the envelope's already-parsed `parameters`.
    internal const string ParamsItemKey = "packet.parameters";

    private const string ScopeItemKey = "account.scope";

    /// Narrow SELECTs for the packet-storm routes. `accounts` carries fourteen jsonb
    /// documents; an MD or Railway handler reads exactly one of them, so the unscoped
    /// ResolveAsync below makes Postgres detoast and ship the other thirteen on every
    /// single packet of a run. Each entry names its column twice - once for the
    /// projection EF turns into `SELECT "Id", "<col>"`, once for SaveAsync's guard.
    private static readonly Dictionary<SaveColumn, (string Name, Expression<Func<Account, Account>> Project)> Scopes = new()
    {
        [SaveColumn.Md] = (nameof(Account.MdSaveInfo), a => new Account { Id = a.Id, MdSaveInfo = a.MdSaveInfo }),
        [SaveColumn.StoryMd] = (nameof(Account.StoryMdSaveInfo), a => new Account { Id = a.Id, StoryMdSaveInfo = a.StoryMdSaveInfo }),
        [SaveColumn.Railway] = (nameof(Account.RailwaySaveInfo), a => new Account { Id = a.Id, RailwaySaveInfo = a.RailwaySaveInfo }),
        [SaveColumn.Story] = (nameof(Account.StorySaveInfo), a => new Account { Id = a.Id, StorySaveInfo = a.StorySaveInfo }),
        [SaveColumn.Chapter] = (nameof(Account.ChapterState), a => new Account { Id = a.Id, ChapterState = a.ChapterState }),
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

    /// Same, but reads ONLY Id + the named column. Every other property on the returned
    /// Account reads as null, NOT as the stored value, and SaveAsync throws on a write to
    /// one - so a handler needing a second column uses the unscoped overload above
    /// (SelectFormationMirrorDungeon does; it reads CustomIdentities and Personalities).
    ///
    /// Resolve ONCE per request. The stub this tracks stands in for the whole row, so a
    /// second resolve in the same request - scoped or via AccountStore - gets the stub
    /// back from EF's identity resolution rather than a fresh row, and every unloaded
    /// column reads as null.
    public static async Task<Account?> ResolveAsync(HttpContext ctx, SaveColumn column)
    {
        if (ctx.Items["sub"] is not string sub) return null;

        var (name, project) = Scopes[column];
        var db = ctx.RequestServices.GetRequiredService<AppDbContext>();
        var account = await db.Accounts
            .AsNoTracking()
            .Where(a => a.Username == sub)
            .Select(project)
            .SingleOrDefaultAsync();
        if (account is null) return null;

        // Blank the columns the projection didn't load, BEFORE Attach snapshots them.
        // They arrive holding Account's field initializers ("{}", "[]"), so a handler
        // assigning that same placeholder - `account.StorySaveInfo = "{}"` is this
        // codebase's idiom for clearing a save - would leave IsModified false, and the
        // guard in SaveAsync would wave through a write that EF then silently drops.
        // Against a null snapshot any assignment at all registers.
        foreach (var p in StringColumns) if (p.Name != name) p.SetValue(account, null);

        // Attach so change tracking still drives the write: the scoped column carries a
        // real original value, so EF marks it modified iff the handler actually changed
        // it and the UPDATE names that column alone.
        db.Attach(account);
        ctx.Items[ScopeItemKey] = name;
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

        // A scoped account never loaded the other thirteen columns, so writing one would
        // persist nothing while looking like it worked. Fail loudly instead. This catches
        // writes only - a scoped handler READING an unloaded column just sees null, which
        // no guard here can see. (Entries<T>() runs DetectChanges for us.)
        if (ctx.Items[ScopeItemKey] is string scoped
            && db.ChangeTracker.Entries<Account>().SelectMany(e => e.Properties)
                .FirstOrDefault(p => p.IsModified && p.Metadata.Name != scoped)?.Metadata.Name is string stray)
        {
            throw new InvalidOperationException(
                $"Handler wrote Account.{stray} but resolved the account scoped to {scoped}; that " +
                "column was never loaded, so the write would be dropped - use ResolveAsync(ctx).");
        }

        return db.SaveChangesAsync();
    }
}
