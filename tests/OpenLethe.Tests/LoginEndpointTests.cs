using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpenLethe.Data;
using Xunit;

[Collection("postgres")]
public class LoginEndpointTests(PostgresFixture db)
{
    [SkippableFact]
    public async Task Login_CreatesAccount_AndReturnsToken()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString);
        var client = factory.CreateClient();
        var name = $"login_{Guid.NewGuid():N}";

        var resp = await client.PostAsJsonAsync("/auth/login", new { username = name });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("token").GetString()));

        await using var ctx = db.NewContext();
        Assert.NotNull(await new AccountStore(ctx).FindByUsernameAsync(name));
    }

    [SkippableFact]
    public async Task Login_IsIdempotent_SameAccountSameIngameId()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString);
        var client = factory.CreateClient();
        var name = $"login2_{Guid.NewGuid():N}";

        await client.PostAsJsonAsync("/auth/login", new { username = name });
        await client.PostAsJsonAsync("/auth/login", new { username = name });

        await using var ctx = db.NewContext();
        Assert.Single(ctx.Accounts, a => a.Username == name);
    }

    [SkippableFact]
    public async Task Login_RejectsBlankUsername()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString);
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/auth/login", new { username = "" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
