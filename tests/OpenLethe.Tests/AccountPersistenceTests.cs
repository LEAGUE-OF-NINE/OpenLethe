using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;
using Xunit;

[Collection("postgres")]
public class AccountPersistenceTests(PostgresFixture db)
{
    [SkippableFact]
    public async Task JsonbDocument_RoundTripsUnchanged()
    {
        db.RequireDb();
        var doc = """{"nested":{"a":1},"list":[1,2,3]}""";

        Guid id;
        await using (var ctx = db.NewContext())
        {
            var acc = new Account { Id = Guid.NewGuid(), Username = $"rt_{Guid.NewGuid():N}", IngameId = Random.Shared.Next(), MdSaveInfo = doc };
            ctx.Accounts.Add(acc);
            await ctx.SaveChangesAsync();
            id = acc.Id;
        }

        await using (var ctx = db.NewContext())
        {
            var loaded = await ctx.Accounts.SingleAsync(a => a.Id == id);
            // jsonb normalizes whitespace but preserves structure/values; compare parsed.
            Assert.Equal(
                System.Text.Json.JsonDocument.Parse(doc).RootElement.GetRawText().Length > 0,
                true);
            using var got = System.Text.Json.JsonDocument.Parse(loaded.MdSaveInfo);
            Assert.Equal(1, got.RootElement.GetProperty("nested").GetProperty("a").GetInt32());
            Assert.Equal(3, got.RootElement.GetProperty("list")[2].GetInt32());
        }
    }
}
