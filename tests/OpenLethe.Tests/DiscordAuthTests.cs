using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenLethe.Server.Auth;

// The /auth OAuth surface, minus the legs that need Discord itself. What is
// exercised here is everything OpenLethe owns: session-id validation, the cookie
// plant, the authorize-URL shape, and the submit -> poll token handoff.
public class DiscordAuthTests : IClassFixture<DiscordAuthTests.Factory>
{
    // Carries a path segment on purpose: HttpResponseHeaders re-serializes Location
    // as a parsed Uri, which would append "/" to a bare host and make the assertions
    // about client normalization rather than about us.
    public const string Frontend = "https://frontend.example/crux";

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseSetting("Auth:JwtSecret", "discord-auth-test-secret-discord-auth-test-secret");
            b.UseSetting("FRONTEND_URL", Frontend);
            b.UseSetting("CLIENT_ID", "test-client-id");
            b.UseSetting("CLIENT_SECRET", "test-client-secret");
        }
    }

    private readonly Factory _f;
    public DiscordAuthTests(Factory f) => _f = f;

    // Redirects must not be followed - the status and Location header ARE the result.
    private HttpClient Client() =>
        _f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string? Location(HttpResponseMessage r) =>
        r.Headers.TryGetValues("Location", out var v) ? v.Single() : null;

    private static string SessionId() => Guid.NewGuid().ToString("N"); // 32 chars, inside 20..60

    [Fact]
    public async Task Login_WithoutSessionId_RedirectsToFrontend()
    {
        var resp = await Client().GetAsync("/auth/login");

        Assert.Equal(HttpStatusCode.SeeOther, resp.StatusCode);
        Assert.Equal(Frontend, Location(resp));
    }

    [Fact]
    public async Task Login_WithPort_CarriesPortToFrontend()
    {
        var resp = await Client().GetAsync("/auth/login?port=4242");

        Assert.Equal($"{Frontend}?port=4242", Location(resp));
    }

    [Fact]
    public async Task Login_LauncherFlow_PlantsCookieAndGoesStraightToDiscord()
    {
        var session = SessionId();

        var resp = await Client().GetAsync($"/auth/login?session_id={session}&launcher=true");

        Assert.Equal(HttpStatusCode.SeeOther, resp.StatusCode);
        Assert.Equal("/auth/discord?launcher=true", Location(resp));
        Assert.Contains(resp.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith($"session_id={session}") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_LegacyFlow_DetoursViaCrux()
    {
        var resp = await Client().GetAsync($"/auth/login?session_id={SessionId()}");

        Assert.Equal($"{Frontend}/auth", Location(resp));
    }

    [Theory]
    [InlineData("tooshort")]                                                   // < 20
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123")] // > 60
    [InlineData("nonascii-éééééééééééé")]
    public async Task Login_RejectsMalformedSessionId(string session)
    {
        var resp = await Client().GetAsync($"/auth/login?session_id={Uri.EscapeDataString(session)}");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Discord_BuildsAuthorizeUrl_WithPortAndLauncherInState()
    {
        var resp = await Client().GetAsync("/auth/discord?port=7000&launcher=true");

        Assert.Equal(HttpStatusCode.SeeOther, resp.StatusCode);
        var url = Location(resp)!;
        Assert.StartsWith("https://discord.com/api/oauth2/authorize?response_type=code", url);
        Assert.Contains("client_id=test-client-id", url);
        Assert.Contains("scope=identify", url);
        Assert.Contains("state=7000%2Clauncher", url);
        Assert.Contains(Uri.EscapeDataString("http://localhost:8080/auth/authorized"), url);
    }

    [Fact]
    public async Task TokenSubmitThenPoll_ReturnsTheStoredToken()
    {
        var client = Client();
        var session = SessionId();

        var submit = new HttpRequestMessage(HttpMethod.Post, "/auth/token/submit")
        {
            Content = JsonContent.Create(new { token = "jwt-from-the-frontend" }),
        };
        submit.Headers.Add("Cookie", $"session_id={session}");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(submit)).StatusCode);

        var poll = await client.PostAsJsonAsync("/auth/token/poll", new { session_id = session });

        Assert.Equal(HttpStatusCode.OK, poll.StatusCode);
        using var doc = JsonDocument.Parse(await poll.Content.ReadAsStringAsync());
        Assert.Equal("jwt-from-the-frontend", doc.RootElement.GetProperty("token").GetString());
    }

    [Fact]
    public async Task TokenSubmit_WithoutCookie_IsRejected()
    {
        var resp = await Client().PostAsJsonAsync("/auth/token/submit", new { token = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // The handoff is one-time: a token left collectable for its whole TTL is a
    // second chance for anyone who learns the session id.
    [Fact]
    public async Task TokenPoll_ConsumesTheToken()
    {
        var client = Client();
        var session = SessionId();

        var submit = new HttpRequestMessage(HttpMethod.Post, "/auth/token/submit")
        {
            Content = JsonContent.Create(new { token = "single-use" }),
        };
        submit.Headers.Add("Cookie", $"session_id={session}");
        await client.SendAsync(submit);

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/auth/token/poll", new { session_id = session })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync("/auth/token/poll", new { session_id = session })).StatusCode);
    }

    // Otherwise a captcha cookie renews itself forever and an ephemeral dashboard
    // token widens into one.
    [Fact]
    public async Task Captcha_RejectsNarrowTokens()
    {
        var jwt = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<JwtService>(_f.Services);

        foreach (var token in new[] { jwt.MintCaptcha("someone"), jwt.MintEphemeral("someone") })
        {
            var resp = await Client().PostAsJsonAsync("/auth/captcha",
                new { token, captchaToken = "x" });

            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }
    }

    [Fact]
    public async Task TokenPoll_UnknownSession_Is404()
    {
        var resp = await Client().PostAsJsonAsync("/auth/token/poll", new { session_id = SessionId() });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Captcha_WithoutValidToken_IsUnauthorized()
    {
        var resp = await Client().PostAsJsonAsync("/auth/captcha",
            new { token = "not-a-jwt", captchaToken = "x" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Avatar_WithoutValidToken_FallsBackToDefaultAvatar()
    {
        var resp = await Client().PostAsJsonAsync("/misc/avatar", new { token = "not-a-jwt" });

        Assert.Equal(HttpStatusCode.SeeOther, resp.StatusCode);
        Assert.Equal("https://cdn.discordapp.com/embed/avatars/0.png", Location(resp));
    }

    // /misc must be exempt from the game envelope check, or every route above 401s
    // before its handler runs.
    [Fact]
    public async Task MiscRoutes_AreExemptFromTheGameJwtMiddleware()
    {
        var resp = await Client().PostAsJsonAsync("/misc/avatar", new { token = "not-a-jwt" });

        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}

// The narrow tokens must not double as game credentials.
public class NarrowTokenRejectionTests : IClassFixture<JwtMiddlewareTests.NoDbFactory>
{
    private readonly JwtMiddlewareTests.NoDbFactory _f;
    public NarrowTokenRejectionTests(JwtMiddlewareTests.NoDbFactory f) => _f = f;

    private static object Body(string authCode) => new
    {
        userAuth = new { uid = 1, dbid = 1, authCode, version = "1", synchronousDataVersion = 0 },
        parameters = new { },
    };

    private JwtService Jwt() =>
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<JwtService>(_f.Services);

    // Crux mints this via /dashboard/auth/token and hands it to the game, so it MUST
    // work as a game credential - the short lifetime is the mitigation, not a ban.
    // Rejecting it here broke SignInAsSteam and every subsequent /api call.
    [Fact]
    public async Task EphemeralDashboardToken_IsAValidGameCredential()
    {
        var resp = await _f.CreateClient()
            .PostAsJsonAsync("/api/AcquireAttendanceReward", Body(Jwt().MintEphemeral("anyone")));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task CaptchaCookieToken_IsNotAGameCredential()
    {
        var resp = await _f.CreateClient()
            .PostAsJsonAsync("/api/AcquireAttendanceReward", Body(Jwt().MintCaptcha("anyone")));

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // The ordinary token still works - the guard must not have closed the door.
    [Fact]
    public async Task PlainToken_StillWorks()
    {
        var resp = await _f.CreateClient()
            .PostAsJsonAsync("/api/AcquireAttendanceReward", Body(Jwt().Mint("anyone")));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}

public class DashboardTokenScopeTests : IClassFixture<JwtMiddlewareTests.NoDbFactory>
{
    private readonly JwtMiddlewareTests.NoDbFactory _f;
    public DashboardTokenScopeTests(JwtMiddlewareTests.NoDbFactory f) => _f = f;

    private JwtService Jwt() =>
        Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<JwtService>(_f.Services);

    [Fact]
    public async Task CaptchaToken_IsNotADashboardCredential()
    {
        var resp = await _f.CreateClient()
            .PostAsJsonAsync("/dashboard/userinfo", new { token = Jwt().MintCaptcha("anyone") });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // Ephemeral must still reach the account lookup - handing the frontend a
    // short-lived token is the whole point of /dashboard/auth/token. That needs a
    // database to assert meaningfully, so it lives in DashboardEndpointTests
    // (`AuthToken_MintsEphemeral_...`) rather than being faked here.
}

public class JwtProfileClaimTests
{
    private static JwtService Svc() => new("profile-claim-test-secret-profile-claim-test", TimeSpan.FromHours(1));

    [Fact]
    public void MintProfile_RoundTripsNameAndAvatar()
    {
        var svc = Svc();

        Assert.True(svc.TryVerifyClaims(svc.MintProfile("1234", "someone", "abc.png"), out var claims));
        Assert.Equal("1234", claims.Sub);
        Assert.Equal("someone", claims.Name);
        Assert.Equal("abc.png", claims.Avatar);
        Assert.False(claims.Captcha);
    }

    [Fact]
    public void MintCaptcha_SetsCaptchaFlag_AndPlainMintDoesNot()
    {
        var svc = Svc();

        Assert.True(svc.TryVerifyClaims(svc.MintCaptcha("1234"), out var captcha));
        Assert.True(captcha.Captcha);

        Assert.True(svc.TryVerifyClaims(svc.Mint("1234"), out var plain));
        Assert.False(plain.Captcha);
        Assert.Equal("", plain.Name);
    }

    // Existing game tokens must not change shape now that the payload has more fields.
    [Fact]
    public void PlainMint_OmitsTheNewClaims()
    {
        var token = Svc().Mint("1234");
        var payload = System.Text.Encoding.UTF8.GetString(
            System.Buffers.Text.Base64Url.DecodeFromChars(token.Split('.')[1]));

        Assert.DoesNotContain("name", payload);
        Assert.DoesNotContain("avatar", payload);
        Assert.DoesNotContain("captcha", payload);
    }
}
