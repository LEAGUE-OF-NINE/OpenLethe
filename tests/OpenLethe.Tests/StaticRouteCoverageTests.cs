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
        // The generator (tools/extract-static-routes.ps1) is client-driven: a
        // route/type pair comes from the packets/api_*.cs header comments, and
        // Rust (router.rs + handler source) only supplies the stateless
        // classification (static_response present, UserRepository absent).
        // Against the current client + lethe-server source that resolves 115
        // routes whose Rust handler is directly proven stateless, plus 4 more
        // client-only *v2 routes (EnterThreadDungeonv2, ExitThreadDungeonv2,
        // SkipExpDungeonv2, SkipThreadDungeonv2) that Rust has never heard of
        // but that share a packet contract with a proven-stateless Rust route
        // (e.g. EnterThreadDungeonv2 reuses EnterThreadDungeon's ReqPacket_/
        // ResPacket_ pair) - 115 + 4 = 119. Five routes Rust declares and
        // classifies stateless but the client does not declare at all
        // (EgoGacksung, EnterMirrordungeonMapNodeBattleAfterChoice,
        // EnterStoryDungeonMapNodeBattleAfterChoice, GetStoryDungeonNodeRecord,
        // PersonalityGacksung) are commented out in StaticRoutes.cs as
        // `// NOT IN CLIENT:` rather than invented. Three further routes
        // (login/GetTermsOfUseStateAll, /api/GetMirrorDungeonEgoGiftRecord,
        // /api/ExitMirrorDungeon) use static_response with NON-default data the
        // generic MapPacket can't reproduce, so they're force-excluded and
        // served by real handlers (Program.cs) instead.
        Assert.Equal(119, StaticRoutes.RegisteredCount);
    }

    [Fact]
    public void Application_BootsWithoutError()
    {
        // MapPacket resolves each packet ID once at startup via ResolvePacketId,
        // which now defaults to 0 on a miss instead of throwing (the client
        // ignores packetId) - so a missing constant can never block boot. This
        // just confirms the app still starts cleanly with all 119 routes wired.
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

    [Theory]
    // Regression for the live bug this cycle fixes: the client's api_*v2.cs
    // packet files declare their classes WITHOUT the v2 suffix (e.g.
    // packets/api_EnterExpDungeonv2.cs declares ReqPacket_EnterExpDungeon, not
    // ReqPacket_EnterExpDungeonv2). The old generator derived types from the
    // route name (route -> ReqPacket_<route>), so it could never match these
    // classes and the six routes below 404'd against real client traffic.
    [InlineData("/api/EnterExpDungeonv2")]
    [InlineData("/api/ExitExpDungeonv2")]
    [InlineData("/api/SkipExpDungeonv2")]
    [InlineData("/api/EnterThreadDungeonv2")]
    [InlineData("/api/ExitThreadDungeonv2")]
    [InlineData("/api/SkipThreadDungeonv2")]
    public async Task V2DungeonRoutes_AreNotNotFound(string route)
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync(route, new
        {
            userAuth = new { uid = 1, dbid = 1, authCode = _token, version = "1", synchronousDataVersion = 0 },
            parameters = new { },
        });

        // Not asserting a specific success code (some of these may yet need
        // auth/session wiring) - the bug being fixed is routing (404), so that
        // is the only thing this test is about.
        Assert.NotEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
