using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Server.Auth;
using Xunit;

// Rust wraps the whole app in TimeoutLayer(15s) and RequestBodyLimitLayer(2MB).
// Both only start mattering once the server is publicly reachable.
//
// Only the timeout is covered here. The body limit is a Kestrel setting, and
// WebApplicationFactory runs on TestServer, which has no Kestrel and ignores it -
// contorting the production code into a middleware just to make it unit-testable
// would be the wrong trade. It is verified against the real container instead.
public class RequestLimitsTests
{
    private const string Secret = "limits-test-secret-limits-test-secret-limits!";

    private sealed class Factory(double timeoutSeconds, TimeSpan upstreamDelay)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseSetting("Auth:JwtSecret", Secret);
            b.UseSetting("REQUEST_TIMEOUT_SECONDS", timeoutSeconds.ToString());
            // Makes /misc/avatar call out to "Discord", which our stub answers slowly.
            b.UseSetting("DISCORD_GUILD_ID", "123");
            b.ConfigureServices(s =>
                s.AddSingleton<IHttpClientFactory>(new SlowHttpClientFactory(upstreamDelay)));
        }
    }

    /// Stands in for a hung upstream. Never touches the network.
    private sealed class SlowHttpClientFactory(TimeSpan delay) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(delay));

        private sealed class Handler(TimeSpan delay) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken ct)
            {
                await Task.Delay(delay, ct);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"user":{"avatar":"abc"}}""",
                        System.Text.Encoding.UTF8, "application/json"),
                };
            }
        }
    }

    private static string Token(WebApplicationFactory<Program> f) =>
        f.Services.GetRequiredService<JwtService>().Mint("someone");

    [Fact]
    public async Task HungUpstream_Returns504_RatherThanHangingForever()
    {
        using var f = new Factory(timeoutSeconds: 1, upstreamDelay: TimeSpan.FromSeconds(30));

        var resp = await f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
            .PostAsJsonAsync("/misc/avatar", new { token = Token(f) });

        Assert.Equal(HttpStatusCode.GatewayTimeout, resp.StatusCode);
    }

    // The control: the same path with a responsive upstream must not be cut off,
    // or the test above would pass for the wrong reason.
    [Fact]
    public async Task ResponsiveUpstream_IsNotTimedOut()
    {
        using var f = new Factory(timeoutSeconds: 30, upstreamDelay: TimeSpan.Zero);

        var resp = await f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
            .PostAsJsonAsync("/misc/avatar", new { token = Token(f) });

        Assert.NotEqual(HttpStatusCode.GatewayTimeout, resp.StatusCode);
        Assert.Equal(HttpStatusCode.SeeOther, resp.StatusCode);
    }

    // Launcher payloads stream multi-megabyte files; 15s is a handler budget, not a
    // download budget, so those routes opt out.
    [Fact]
    public async Task ModFileRoutes_AreExemptFromTheTimeout()
    {
        using var f = new Factory(timeoutSeconds: 1, upstreamDelay: TimeSpan.FromSeconds(30));

        // noticeMeta.json needs no file on disk and no upstream call, so a 504 here
        // could only come from the timeout policy applying where it should not.
        var resp = await f.CreateClient().GetAsync("/noticeMeta.json");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task OrdinaryRequests_AreUnaffected()
    {
        using var f = new Factory(timeoutSeconds: 15, upstreamDelay: TimeSpan.Zero);

        Assert.Equal("ok", await f.CreateClient().GetStringAsync("/health"));
    }
}
