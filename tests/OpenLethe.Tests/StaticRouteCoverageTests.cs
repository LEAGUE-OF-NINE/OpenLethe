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
        // route/type pair comes from the packet header comments, and Rust
        // (router.rs + handler source) only supplies the stateless
        // classification (static_response present, UserRepository absent).
        // Against the current client + lethe-server source that resolves:
        //   115  /api + /login routes whose Rust handler is directly proven stateless
        // +   4  client-only *v2 routes Rust has never heard of, admitted because
        //        they share a packet contract with a proven-stateless Rust route
        //        (EnterThreadDungeonv2, ExitThreadDungeonv2, SkipExpDungeonv2,
        //        SkipThreadDungeonv2)
        // +  13  /iap and /log routes, brought into the generator's scope in cycle 7
        //        (9 /iap + 4 /log, all Rust-declared and all proven stateless)
        // = 132.
        // /iap/PurchaseAsAppleV2 is client-only and is NOT admitted: no Rust-static
        // route shares its packet pair, and its response carries an appleIAP object a
        // canned default cannot fill. Five routes Rust declares and classifies
        // stateless but the client does not declare at all (EgoGacksung,
        // EnterMirrordungeonMapNodeBattleAfterChoice,
        // EnterStoryDungeonMapNodeBattleAfterChoice, GetStoryDungeonNodeRecord,
        // PersonalityGacksung) are commented out as `// NOT IN CLIENT:` rather than
        // invented. Three further routes (login/GetTermsOfUseStateAll,
        // /api/GetMirrorDungeonEgoGiftRecord, /api/ExitMirrorDungeon) use
        // static_response with NON-default data the generic MapPacket can't
        // reproduce, so they're force-excluded and served by real handlers instead.
        Assert.Equal(132, StaticRoutes.RegisteredCount);
    }

    [Fact]
    public void Application_BootsWithoutError()
    {
        // MapPacket resolves each packet ID once at startup via ResolvePacketId,
        // which now defaults to 0 on a miss instead of throwing (the client
        // ignores packetId) - so a missing constant can never block boot. This
        // just confirms the app still starts cleanly with all 132 routes wired.
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
    // All six go through the same auth-free MapPacket path as
    // SampledRoutes_ReturnOkEnvelope above, so - now that routing is fixed -
    // they can be held to the same 200/"ok" envelope assertion, not just
    // "not a 404".
    [InlineData("/api/EnterExpDungeonv2")]
    [InlineData("/api/ExitExpDungeonv2")]
    [InlineData("/api/SkipExpDungeonv2")]
    [InlineData("/api/EnterThreadDungeonv2")]
    [InlineData("/api/ExitThreadDungeonv2")]
    [InlineData("/api/SkipThreadDungeonv2")]
    public async Task V2DungeonRoutes_ReturnOkEnvelope(string route)
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
    // Cycle 7 brought the /iap and /log groups into the generator's scope. All are
    // static_response stubs with no UserRepository even in the Rust reference (there
    // is no real payment integration upstream). packetId is deliberately NOT asserted
    // here: only 5 of the 13 have a Rust HasPacketId impl (GetGachaLogAll, GetMailLogAll,
    // GetSteamWalletCurrency, InitPurchase, UpdateSteamPendingPurchase), so the other 8
    // resolve to 0 by design - the client ignores the field. See ResolvePacketId.
    [InlineData("/iap/Purchase")]
    [InlineData("/iap/InitPurchase")]
    [InlineData("/iap/PurchaseIngameProduct")]
    [InlineData("/log/ReportSpeedHack")]
    [InlineData("/log/GetGachaLogAll")]
    public async Task IapAndLogRoutes_ReturnOkEnvelope(string route)
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
    }
}
