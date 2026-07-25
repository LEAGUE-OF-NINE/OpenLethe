using Microsoft.EntityFrameworkCore;

namespace OpenLethe.Data;

public sealed class AccountStore(AppDbContext ctx)
{
    public Task<Account?> FindByUsernameAsync(string username, CancellationToken ct = default) =>
        ctx.Accounts.SingleOrDefaultAsync(a => a.Username == username, ct);

    public async Task<Account> GetOrCreateByUsernameAsync(string username, CancellationToken ct = default)
    {
        var existing = await FindByUsernameAsync(username, ct);
        if (existing is not null) return existing;

        // ponytail: naive max+1 id assignment; a localhost server has no concurrent
        // writers. Add a sequence/allocation guard only if multi-writer becomes real.
        var nextIngameId = (await ctx.Accounts.MaxAsync(a => (int?)a.IngameId, ct) ?? 0) + 1;

        var now = DateTime.UtcNow;
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Username = username,
            IngameId = nextIngameId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync(ct);
        return account;
    }
}
