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
        return await CreateAsync(discordId, ct, discordId);
    }

    /// Null when the username belongs to a Discord-linked account. Those are
    /// reachable only through the OAuth flow: /auth/login takes any username on
    /// trust, so without this guard anyone could name a Discord user's account and
    /// be handed a token that resolves to it.
    public async Task<Account?> GetOrCreateByUsernameAsync(string username, CancellationToken ct = default)
    {
        var existing = await FindByUsernameAsync(username, ct);
        if (existing is not null) return existing.DiscordId is null ? existing : null;

        return await CreateAsync(username, ct);
    }

    private async Task<Account> CreateAsync(string username, CancellationToken ct, string? discordId = null)
    {
        // ponytail: naive max+1 id assignment; a localhost server has no concurrent
        // writers. Add a sequence/allocation guard only if multi-writer becomes real.
        var nextIngameId = (await ctx.Accounts.MaxAsync(a => (int?)a.IngameId, ct) ?? 0) + 1;

        var now = DateTime.UtcNow;
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Username = username,
            DiscordId = discordId,
            IngameId = nextIngameId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync(ct);
        return account;
    }
}
