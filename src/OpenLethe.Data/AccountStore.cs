using Microsoft.EntityFrameworkCore;

namespace OpenLethe.Data;

public sealed class AccountStore(AppDbContext ctx)
{
    public Task<Account?> FindByUsernameAsync(string username, CancellationToken ct = default) =>
        ctx.Accounts.SingleOrDefaultAsync(a => a.Username == username, ct);

    public Task<Account?> FindByDiscordIdAsync(string discordId, CancellationToken ct = default) =>
        ctx.Accounts.SingleOrDefaultAsync(a => a.DiscordId == discordId, ct);

    /// Rust login_authorized: get_by_discord_id, else create.
    ///
    /// The account is named by the SNOWFLAKE, not by the Discord display name.
    /// Username is this server's identity key - it is what every handler resolves
    /// the JWT `sub` against - so it has to be unique and stable, which a display
    /// name is neither. The display name travels in the token's `name` claim, which
    /// is where Rust keeps it too.
    ///
    /// Null when a non-Discord account already squats that username; the caller
    /// must not adopt it, or the squatter's token would resolve to this account.
    public async Task<Account?> GetOrCreateByDiscordIdAsync(
        string discordId, CancellationToken ct = default)
    {
        var existing = await FindByDiscordIdAsync(discordId, ct);
        if (existing is not null) return existing;

        if (await FindByUsernameAsync(discordId, ct) is not null) return null;

        // One save, not two: a row written without its DiscordId would be an
        // unclaimed account named after the snowflake, which /auth/login would then
        // hand to anyone asking for it.
        var created = await CreateAsync(discordId, ct, discordId);
        if (created is not null) return created;

        // Lost the insert race to a concurrent first login; their row is ours too.
        return await FindByDiscordIdAsync(discordId, ct);
    }

    /// Null when the username belongs to a Discord-linked account. Those are
    /// reachable only through the OAuth flow: /auth/login takes any username on
    /// trust, so without this guard anyone could name a Discord user's account and
    /// be handed a token that resolves to it.
    public async Task<Account?> GetOrCreateByUsernameAsync(string username, CancellationToken ct = default)
    {
        var existing = await FindByUsernameAsync(username, ct);
        if (existing is not null) return existing.DiscordId is null ? existing : null;

        var created = await CreateAsync(username, ct);
        if (created is not null) return created;

        // Lost the insert race to a concurrent first login; re-apply the same rule.
        existing = await FindByUsernameAsync(username, ct);
        return existing?.DiscordId is null ? existing : null;
    }

    /// Null on a unique-key collision: a concurrent request created the row between
    /// our find and our insert. Callers re-fetch; the unique indexes guarantee the
    /// winner's row is the one they get. IngameId itself is DB-generated (identity),
    /// so ids never collide no matter how many writers race.
    private async Task<Account?> CreateAsync(string username, CancellationToken ct, string? discordId = null)
    {
        var now = DateTime.UtcNow;
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Username = username,
            DiscordId = discordId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        ctx.Accounts.Add(account);
        try
        {
            await ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (
            e.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // Detach the failed insert so this context stays usable for the re-fetch.
            ctx.Entry(account).State = EntityState.Detached;
            return null;
        }
        return account;
    }
}
