using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

// The Crux frontend is a separate origin and sends credentials, so without these
// headers the browser blocks every dashboard call before it leaves the page.
public class CorsTests
{
    private const string Frontend = "http://localhost:5173";

    private sealed class Factory(string? frontendUrl) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseSetting("Auth:JwtSecret", "cors-test-secret-cors-test-secret-cors-test");
            if (frontendUrl is not null) b.UseSetting("FRONTEND_URL", frontendUrl);
        }
    }

    private static HttpRequestMessage Preflight(string path, string origin)
    {
        var req = new HttpRequestMessage(HttpMethod.Options, path);
        req.Headers.Add("Origin", origin);
        req.Headers.Add("Access-Control-Request-Method", "POST");
        req.Headers.Add("Access-Control-Request-Headers", "content-type");
        return req;
    }

    private static string? Header(HttpResponseMessage r, string name) =>
        r.Headers.TryGetValues(name, out var v) ? string.Join(",", v) : null;

    [Fact]
    public async Task Preflight_FromTheFrontend_IsAllowedWithCredentials()
    {
        using var f = new Factory(Frontend);

        var resp = await f.CreateClient().SendAsync(Preflight("/dashboard/auth/token", Frontend));

        Assert.Equal(Frontend, Header(resp, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Header(resp, "Access-Control-Allow-Credentials"));
        Assert.Contains("POST", Header(resp, "Access-Control-Allow-Methods") ?? "");
    }

    // A preflight has no packet envelope; if it reached JwtAuthMiddleware it would
    // 400 and the browser would report a CORS error instead of the real one.
    [Fact]
    public async Task Preflight_IsNotRejectedByTheGameMiddleware()
    {
        using var f = new Factory(Frontend);

        var resp = await f.CreateClient().SendAsync(Preflight("/api/AcquireAttendanceReward", Frontend));

        Assert.NotEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal(Frontend, Header(resp, "Access-Control-Allow-Origin"));
    }

    // An actual (non-preflight) request must carry the header too, or fetch() still
    // rejects the response after it arrives.
    [Fact]
    public async Task ActualRequest_CarriesTheAllowOriginHeader()
    {
        using var f = new Factory(Frontend);

        var req = new HttpRequestMessage(HttpMethod.Post, "/dashboard/auth/token")
        {
            Content = JsonContent.Create(new { token = "irrelevant" }),
        };
        req.Headers.Add("Origin", Frontend);

        var resp = await f.CreateClient().SendAsync(req);

        Assert.Equal(Frontend, Header(resp, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task OtherOrigins_AreNotAllowed()
    {
        using var f = new Factory(Frontend);

        var resp = await f.CreateClient().SendAsync(Preflight("/dashboard/auth/token", "http://evil.example"));

        Assert.Null(Header(resp, "Access-Control-Allow-Origin"));
    }

    // Rust unwraps a missing FRONTEND_URL and crashes on boot. A self-hosted server
    // with no frontend must still start; it just allows no cross-origin caller.
    [Fact]
    public async Task NoFrontendConfigured_StillBoots_AndSendsNoCorsHeaders()
    {
        using var f = new Factory(null);
        var client = f.CreateClient();

        Assert.Equal("ok", await client.GetStringAsync("/health"));

        var resp = await client.SendAsync(Preflight("/dashboard/auth/token", Frontend));
        Assert.Null(Header(resp, "Access-Control-Allow-Origin"));
    }

    // FRONTEND_URL doubles as the OAuth redirect target, where a trailing slash is
    // harmless - but an Origin header never has one, so it must be trimmed.
    [Fact]
    public async Task TrailingSlashOnFrontendUrl_StillMatchesTheOrigin()
    {
        using var f = new Factory(Frontend + "/");

        var resp = await f.CreateClient().SendAsync(Preflight("/dashboard/auth/token", Frontend));

        Assert.Equal(Frontend, Header(resp, "Access-Control-Allow-Origin"));
    }
}
