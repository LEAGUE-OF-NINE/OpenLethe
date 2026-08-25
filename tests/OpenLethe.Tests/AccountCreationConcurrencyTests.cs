using OpenLethe.Data;

// Multi-user hardening: concurrent signups must not collide on IngameId
// (DB-generated identity, not max+1) and concurrent first logins for the SAME
// name must converge on one account instead of surfacing a unique violation.
[Collection("postgres")]
public class AccountCreationConcurrencyTests(PostgresFixture db)
{
    private const int Writers = 8;

    [SkippableFact]
    public async Task ParallelSignups_DistinctNames_AllSucceedWithUniqueIngameIds()
    {
        db.RequireDb();
        var stamp = Guid.NewGuid().ToString("N");

        var tasks = Enumerable.Range(0, Writers).Select(async i =>
        {
            await using var ctx = db.NewContext();
            return await new AccountStore(ctx).GetOrCreateByUsernameAsync($"conc_{stamp}_{i}");
        }).ToArray();
        var accounts = await Task.WhenAll(tasks);

        Assert.All(accounts, a => Assert.NotNull(a));
        Assert.Equal(Writers, accounts.Select(a => a!.IngameId).Distinct().Count());
    }

    [SkippableFact]
    public async Task ParallelSignups_SameName_ConvergeOnOneAccount()
    {
        db.RequireDb();
        var name = $"conc_same_{Guid.NewGuid():N}";

        var tasks = Enumerable.Range(0, Writers).Select(async _ =>
        {
            await using var ctx = db.NewContext();
            return await new AccountStore(ctx).GetOrCreateByUsernameAsync(name);
        }).ToArray();
        var accounts = await Task.WhenAll(tasks);

        Assert.All(accounts, a => Assert.NotNull(a));
        Assert.Single(accounts.Select(a => a!.Id).Distinct());

        await using var check = db.NewContext();
        Assert.Single(check.Accounts, a => a.Username == name);
    }
}
