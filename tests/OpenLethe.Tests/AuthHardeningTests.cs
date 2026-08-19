using System.Net;
using System.Net.Http.Json;
using Xunit;

// Public-deployment hardening: /auth/login is trust-on-first-use and must be
// switchable off, and the unauthenticated /auth surface must be rate-limited.
[Collection("postgres")]
public class AuthHardeningTests(PostgresFixture db)
{
    [SkippableFact]
    public async Task Login_NotFound_WhenLocalLoginDisabled()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString,
            ("Auth:EnableLocalLogin", "false"));
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/auth/login", new { username = "someone" });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [SkippableFact]
    public async Task Auth_RateLimited_AfterConfiguredPermits()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString,
            ("Auth:RateLimitPerMinute", "2"));
        var client = factory.CreateClient();

        var r1 = await client.PostAsJsonAsync("/auth/login", new { username = "" });
        var r2 = await client.PostAsJsonAsync("/auth/login", new { username = "" });
        var r3 = await client.PostAsJsonAsync("/auth/login", new { username = "" });

        Assert.Equal(HttpStatusCode.BadRequest, r1.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, r2.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, r3.StatusCode);
    }

    [SkippableFact]
    public async Task NonAuthRoutes_AreNotRateLimited()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString,
            ("Auth:RateLimitPerMinute", "2"));
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var resp = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }
    }
}
