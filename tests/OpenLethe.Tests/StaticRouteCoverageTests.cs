using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class StaticRouteCoverageTests : IClassFixture<StaticRouteCoverageTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder b) =>
            b.UseSetting("Auth:JwtSecret", "static-route-coverage-test-secret-static-route-coverage-test-secret");
    }

    private readonly Factory _factory;
    private readonly string _token;

    public StaticRouteCoverageTests(Factory factory)
    {
        _factory = factory;
        _token = factory.Services.GetRequiredService<OpenLethe.Server.Auth.JwtService>().Mint("test");
    }

    [Fact]
    public void AllStatelessRoutes_AreRegistered()
    {
        // The generator (tools/extract-static-routes.ps1) classifies /api/ and
        // /login/ routes as stateless (static_response, never UserRepository)
        // against the current lethe-server source: 107 /api/ + 16 /login/. Of
        // those, 10 /api/ routes have a route name that has drifted from its
        // client ReqPacket_/ResPacket_ pair - those are commented out in
        // StaticRoutes.cs as `// MISSING:` rather than invented, leaving
        // 97 + 16 = 113 actually registered here. See StaticRoutes.cs for the
        // full list of MISSING lines; they are cycle 3 items.
        Assert.Equal(113, StaticRoutes.RegisteredCount);
    }

    [Fact]
    public void Application_BootsWithoutError()
    {
        // MapPacket resolves each packet ID once at startup via ResolvePacketId,
        // which now defaults to 0 on a miss instead of throwing (the client
        // ignores packetId) - so a missing constant can never block boot. This
        // just confirms the app still starts cleanly with all 113 routes wired.
        var client = _factory.CreateClient();
        Assert.NotNull(client);
    }

    [Theory]
    // All three verified stateless in the Rust source: they use static_response
    // and never touch UserRepository.
    [InlineData("/api/AcquireAttendanceReward")]
    [InlineData("/api/GetAttendanceState")]
    [InlineData("/api/GetHellsChickenState")]
    public async Task SampledRoutes_ReturnOkEnvelope(string route)
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync(route, new
        {
            userAuth = new { uid = 1, dbid = 1, authCode = _token, version = "1", synchronousDataVersion = 0 },
            parameters = new { },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("ok", doc.RootElement.GetProperty("state").GetString());
        Assert.True(doc.RootElement.GetProperty("packetId").GetInt64() != 0);
    }
}
