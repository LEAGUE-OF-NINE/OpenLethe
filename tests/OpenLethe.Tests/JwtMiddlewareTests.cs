using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Server.Auth;
using Xunit;

// These need no database: middleware is signature-only, and the stateless
// static routes ignore the account. Uses a DB-free factory with a fixed secret.
// (Tests are [SkippableFact] for consistency with the DB tests; they never skip,
// since they call no Skip.* — [SkippableFact] is a drop-in superset of [Fact].)
public class JwtMiddlewareTests : IClassFixture<JwtMiddlewareTests.NoDbFactory>
{
    public sealed class NoDbFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder b)
        {
            // No connection string -> no startup migration, DB-free boot.
            b.UseSetting("Auth:JwtSecret", "middleware-test-secret-middleware-test-secret");
        }
    }

    private readonly NoDbFactory _factory;
    public JwtMiddlewareTests(NoDbFactory factory) => _factory = factory;

    private object Body(string authCode) => new
    {
        userAuth = new { uid = 1, dbid = 1, authCode, version = "1", synchronousDataVersion = 0 },
        parameters = new { },
    };

    [SkippableFact]
    public async Task ProtectedApiRoute_WithValidToken_Returns200()
    {
        var token = _factory.Services.GetRequiredService<JwtService>().Mint("anyone");
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/AcquireAttendanceReward", Body(token));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [SkippableFact]
    public async Task ProtectedApiRoute_WithInvalidToken_Returns401()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/api/AcquireAttendanceReward", Body("garbage"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [SkippableFact]
    public async Task LoginRoute_IsExempt_NoTokenNeeded()
    {
        var resp = await _factory.CreateClient().PostAsJsonAsync("/login/CheckClientVersion", Body(""));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [SkippableFact]
    public async Task ProtectedApiRoute_WithUnparseableBody_Returns400()
    {
        var resp = await _factory.CreateClient().PostAsync("/api/AcquireAttendanceReward",
            new StringContent("{not json", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
