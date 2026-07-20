using OpenLethe.Data;
using Xunit;

[Collection("postgres")]
public class AccountStoreTests(PostgresFixture db)
{
    [SkippableFact]
    public async Task GetOrCreate_IsIdempotent_ForSameUsername()
    {
        db.RequireDb();
        var name = $"u_{Guid.NewGuid():N}";

        await using var ctx1 = db.NewContext();
        var first = await new AccountStore(ctx1).GetOrCreateByUsernameAsync(name);

        await using var ctx2 = db.NewContext();
        var second = await new AccountStore(ctx2).GetOrCreateByUsernameAsync(name);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.IngameId, second.IngameId);
    }

    [SkippableFact]
    public async Task GetOrCreate_AssignsDistinctIngameIds()
    {
        db.RequireDb();
        await using var ctx = db.NewContext();
        var store = new AccountStore(ctx);

        var a = await store.GetOrCreateByUsernameAsync($"a_{Guid.NewGuid():N}");
        var b = await store.GetOrCreateByUsernameAsync($"b_{Guid.NewGuid():N}");

        Assert.NotEqual(a.IngameId, b.IngameId);
    }

    [SkippableFact]
    public async Task Find_ReturnsNull_ForUnknownUsername()
    {
        db.RequireDb();
        await using var ctx = db.NewContext();
        Assert.Null(await new AccountStore(ctx).FindByUsernameAsync($"missing_{Guid.NewGuid():N}"));
    }
}
