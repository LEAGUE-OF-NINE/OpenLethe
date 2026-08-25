using OpenLethe.Data;

// The account half of the OAuth callback. The invariant under test is that a
// Discord login produces a row whose Username is the snowflake - because Username
// is what every handler resolves the JWT `sub` against, so anything else yields a
// token that matches no account.
[Collection("postgres")]
public class DiscordAccountTests(PostgresFixture db)
{
    private static string Snowflake() => Random.Shared.NextInt64(1, long.MaxValue).ToString();

    [SkippableFact]
    public async Task GetOrCreateByDiscordId_NamesTheAccountAfterTheSnowflake()
    {
        db.RequireDb();
        var discordId = Snowflake();

        await using var ctx = db.NewContext();
        var store = new AccountStore(ctx);

        var account = await store.GetOrCreateByDiscordIdAsync(discordId);

        Assert.NotNull(account);
        Assert.Equal(discordId, account.Username);
        Assert.Equal(discordId, account.DiscordId);
        // This is the lookup every game handler and dashboard route performs.
        Assert.NotNull(await store.FindByUsernameAsync(account.Username));
    }

    [SkippableFact]
    public async Task GetOrCreateByDiscordId_IsIdempotent()
    {
        db.RequireDb();
        var discordId = Snowflake();

        await using var ctx = db.NewContext();
        var store = new AccountStore(ctx);

        var first = await store.GetOrCreateByDiscordIdAsync(discordId);
        var second = await store.GetOrCreateByDiscordIdAsync(discordId);

        Assert.NotNull(first);
        Assert.Equal(first.Id, second!.Id);
        Assert.Single(ctx.Accounts, a => a.DiscordId == discordId);
    }

    // The dev login must not be able to name a Discord account and be handed a
    // token that resolves to it.
    [SkippableFact]
    public async Task GetOrCreateByUsername_RefusesADiscordLinkedAccount()
    {
        db.RequireDb();
        var discordId = Snowflake();

        await using var ctx = db.NewContext();
        var store = new AccountStore(ctx);

        var owned = await store.GetOrCreateByDiscordIdAsync(discordId);
        Assert.NotNull(owned);

        Assert.Null(await store.GetOrCreateByUsernameAsync(discordId));
        // And no second row was created as a side effect.
        Assert.Single(ctx.Accounts, a => a.Username == discordId);
    }

    // The mirror case: a squatted username is not adopted by the OAuth flow either.
    [SkippableFact]
    public async Task GetOrCreateByDiscordId_RefusesASquattedUsername()
    {
        db.RequireDb();
        var discordId = Snowflake();

        await using var ctx = db.NewContext();
        var store = new AccountStore(ctx);

        var squatter = await store.GetOrCreateByUsernameAsync(discordId);
        Assert.NotNull(squatter);
        Assert.Null(squatter.DiscordId);

        Assert.Null(await store.GetOrCreateByDiscordIdAsync(discordId));
    }

    // Dev-login accounts leave DiscordId null; the unique index is filtered so any
    // number of them can coexist.
    [SkippableFact]
    public async Task DevLoginAccounts_ShareANullDiscordId()
    {
        db.RequireDb();

        await using var ctx = db.NewContext();
        var store = new AccountStore(ctx);

        var a = await store.GetOrCreateByUsernameAsync($"dev_{Guid.NewGuid():N}");
        var b = await store.GetOrCreateByUsernameAsync($"dev_{Guid.NewGuid():N}");

        Assert.Null(a?.DiscordId);
        Assert.Null(b?.DiscordId);
    }
}
