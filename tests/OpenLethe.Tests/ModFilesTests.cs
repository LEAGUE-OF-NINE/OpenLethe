using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

// LetheLauncher GETs these from the server root before the user has logged in, so
// they must serve without a token and 404 informatively when the operator has not
// supplied a file yet.
public class ModFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "openlethe-modfiles-" + Guid.NewGuid().ToString("N"));

    private readonly Factory _f;

    public ModFilesTests()
    {
        Directory.CreateDirectory(_dir);
        _f = new Factory(_dir);
    }

    public void Dispose()
    {
        _f.Dispose();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    public sealed class Factory(string dir) : WebApplicationFactory<Program>
    {
        /// Settings applied on top of the defaults, per test.
        public Dictionary<string, string> Settings { get; } = new();

        /// Stands in for Discord. Null means "no stub registered".
        public FakeDiscord? Discord { get; set; }

        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseSetting("Auth:JwtSecret", "modfiles-test-secret-modfiles-test-secret");
            b.UseSetting("MOD_FILES_DIR", dir);
            foreach (var (k, v) in Settings) b.UseSetting(k, v);

            if (Discord is not null)
                b.ConfigureServices(s => s.AddSingleton<IHttpClientFactory>(Discord));
        }
    }

    /// Replaces IHttpClientFactory so no test reaches the real Discord API. Records
    /// the requests it saw so caching and auth headers can be asserted.
    public sealed class FakeDiscord(HttpStatusCode status, string body) : IHttpClientFactory
    {
        public int Calls;
        public string? LastAuthHeader;
        public string? LastUrl;

        public HttpClient CreateClient(string name) => new(new Handler(this, status, body));

        private sealed class Handler(FakeDiscord owner, HttpStatusCode status, string body)
            : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken ct)
            {
                owner.Calls++;
                owner.LastUrl = request.RequestUri?.ToString();
                owner.LastAuthHeader = request.Headers.TryGetValues("Authorization", out var v)
                    ? string.Join(",", v) : null;

                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
                });
            }
        }
    }

    private const string ChannelJson = """
        [
          {"attachments":[{"filename":"motions.dll","url":"https://cdn.example/other"}]},
          {"attachments":[{"filename":"Lethe.dll","url":"https://cdn.example/lethe-signed"}]}
        ]
        """;

    [Theory]
    [InlineData("Lethe.dll")]
    [InlineData("ModularSkillScripts.dll")]
    [InlineData("motions.dll")]
    [InlineData("limbus-manifest.txt")]
    [InlineData("noticeMeta.json")]
    public async Task ServesTheFile_WhenPresent(string name)
    {
        var payload = new byte[] { 0x4D, 0x5A, 0x90, 0x00, (byte)name.Length };
        await File.WriteAllBytesAsync(Path.Combine(_dir, name), payload);

        var resp = await _f.CreateClient().GetAsync("/" + name);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(payload, await resp.Content.ReadAsByteArrayAsync());
    }

    // The launcher has no token at this point, so a 401 would break it outright.
    [Fact]
    public async Task NeedsNoAuth()
    {
        await File.WriteAllBytesAsync(Path.Combine(_dir, "Lethe.dll"), [1, 2, 3]);

        var resp = await _f.CreateClient().GetAsync("/Lethe.dll");

        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Missing_Is404_AndSaysWhereToPutIt()
    {
        var resp = await _f.CreateClient().GetAsync("/motions.dll");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("motions.dll", body);
        Assert.Contains(_dir, body);          // tells the operator the directory
        Assert.Contains("MOD_FILES_DIR", body);
    }

    [Fact]
    public async Task DllsAreServedAsBinary_MetadataByItsOwnType()
    {
        foreach (var name in new[] { "Lethe.dll", "noticeMeta.json", "limbus-manifest.txt" })
            await File.WriteAllBytesAsync(Path.Combine(_dir, name), [0]);

        var client = _f.CreateClient();

        Assert.Equal("application/octet-stream",
            (await client.GetAsync("/Lethe.dll")).Content.Headers.ContentType?.MediaType);
        Assert.Equal("application/json",
            (await client.GetAsync("/noticeMeta.json")).Content.Headers.ContentType?.MediaType);
        Assert.Equal("text/plain",
            (await client.GetAsync("/limbus-manifest.txt")).Content.Headers.ContentType?.MediaType);
    }

    // Only the five known names are routed, so nothing else in the directory is
    // reachable and no request-supplied path ever reaches the filesystem.
    [Fact]
    public async Task DoesNotServeArbitraryFilesFromTheDirectory()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "secrets.env"), "CLIENT_SECRET=nope");

        var resp = await _f.CreateClient().GetAsync("/secrets.env");

        // 400, not 404: an unrouted path still reaches JwtAuthMiddleware, which
        // rejects it for having no packet envelope. What matters here is that the
        // file is neither served nor disclosed.
        Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.DoesNotContain("nope", await resp.Content.ReadAsStringAsync());
    }

    // --- tier 2: Discord release channel ------------------------------------

    private Factory DiscordFactory(HttpStatusCode status, string body, FakeDiscord? shared = null)
    {
        var f = new Factory(_dir) { Discord = shared ?? new FakeDiscord(status, body) };
        f.Settings["DISCORD_TOKEN"] = "bot-token";
        f.Settings["RELEASE_CHANNEL_ID"] = "111";
        f.Settings["MODULAR_RELEASE_CHANNEL_ID"] = "222";
        return f;
    }

    [Fact]
    public async Task RedirectsToTheChannelAttachment_WhenNoLocalFile()
    {
        using var f = DiscordFactory(HttpStatusCode.OK, ChannelJson);
        var client = f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var resp = await client.GetAsync("/Lethe.dll");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, resp.StatusCode);   // 307, as Rust
        Assert.Equal("https://cdn.example/lethe-signed",
            resp.Headers.GetValues("Location").Single());
        Assert.Equal("Bot bot-token", f.Discord!.LastAuthHeader);
        Assert.Contains("/channels/111/messages", f.Discord.LastUrl);
    }

    [Fact]
    public async Task UsesTheModularChannel_ForModularSkillScripts()
    {
        using var f = DiscordFactory(HttpStatusCode.OK, "[]");
        var client = f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await client.GetAsync("/ModularSkillScripts.dll");

        Assert.Contains("/channels/222/messages", f.Discord!.LastUrl);
    }

    // A fresh clone has no modfiles/ at all - it is gitignored and Docker only
    // creates it for the bind mount. A missing directory must be a 404, not a crash.
    [Fact]
    public async Task MissingDirectory_Is404_NotAnError()
    {
        var absent = Path.Combine(Path.GetTempPath(), "openlethe-absent-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(absent));

        using var f = new Factory(absent);
        var resp = await f.CreateClient().GetAsync("/Lethe.dll");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        // The defaults must still work with no directory present.
        Assert.Equal(HttpStatusCode.OK, (await f.CreateClient().GetAsync("/noticeMeta.json")).StatusCode);
    }

    [Fact]
    public async Task MotionsUsesItsOwnChannel_WhenSet()
    {
        using var f = DiscordFactory(HttpStatusCode.OK, "[]");
        f.Settings["MOTIONS_RELEASE_CHANNEL_ID"] = "333";
        var client = f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await client.GetAsync("/motions.dll");

        Assert.Contains("/channels/333/messages", f.Discord!.LastUrl);
    }

    // Upstream publishes motions alongside Lethe.dll, so an existing config with
    // only RELEASE_CHANNEL_ID must keep working.
    [Fact]
    public async Task MotionsFallsBackToTheSharedChannel_WhenItsOwnIsUnset()
    {
        using var f = DiscordFactory(HttpStatusCode.OK, "[]");
        var client = f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await client.GetAsync("/motions.dll");

        Assert.Contains("/channels/111/messages", f.Discord!.LastUrl);
    }

    [Fact]
    public async Task MotionsMissing_NamesBothChannelKeys()
    {
        var resp = await _f.CreateClient().GetAsync("/motions.dll");

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("MOTIONS_RELEASE_CHANNEL_ID", body);
        Assert.Contains("RELEASE_CHANNEL_ID", body);
    }

    // A local file is an explicit operator decision and must win.
    [Fact]
    public async Task LocalFileBeatsDiscord()
    {
        await File.WriteAllBytesAsync(Path.Combine(_dir, "Lethe.dll"), [9, 9, 9]);
        using var f = DiscordFactory(HttpStatusCode.OK, ChannelJson);

        var resp = await f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
            .GetAsync("/Lethe.dll");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(new byte[] { 9, 9, 9 }, await resp.Content.ReadAsByteArrayAsync());
        Assert.Equal(0, f.Discord!.Calls);   // Discord was never consulted
    }

    // Signed CDN links expire, so the lookup is cached briefly - but it IS cached.
    [Fact]
    public async Task CachesTheLookup_AcrossRequests()
    {
        using var f = DiscordFactory(HttpStatusCode.OK, ChannelJson);
        var client = f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await client.GetAsync("/Lethe.dll");
        await client.GetAsync("/Lethe.dll");

        Assert.Equal(1, f.Discord!.Calls);
    }

    // An outage must degrade, not 500.
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "[]")]
    [InlineData(HttpStatusCode.OK, "not json at all")]
    [InlineData(HttpStatusCode.OK, "[]")]
    public async Task FallsThroughTo404_WhenDiscordCannotAnswer(HttpStatusCode status, string body)
    {
        using var f = DiscordFactory(status, body);

        var resp = await f.CreateClient().GetAsync("/Lethe.dll");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Contains("RELEASE_CHANNEL_ID", await resp.Content.ReadAsStringAsync());
    }

    // --- tier 3: upstream defaults ------------------------------------------

    [Fact]
    public async Task NoticeMeta_HasABuiltInDocument()
    {
        var resp = await _f.CreateClient().GetAsync("/noticeMeta.json");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal(200001,
            doc.RootElement.GetProperty("noticeDetailList")[0].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task LimbusManifest_RedirectsUpstream_AndIsOverridable()
    {
        var resp = await _f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
            .GetAsync("/limbus-manifest.txt");

        Assert.Equal(HttpStatusCode.PermanentRedirect, resp.StatusCode);   // 308, as Rust
        Assert.Equal("https://files.lethelc.site/limbus-manifest.txt",
            resp.Headers.GetValues("Location").Single());

        using var custom = new Factory(_dir);
        custom.Settings["LIMBUS_MANIFEST_URL"] = "https://mine.example/manifest.txt";
        var overridden = await custom
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
            .GetAsync("/limbus-manifest.txt");

        Assert.Equal("https://mine.example/manifest.txt",
            overridden.Headers.GetValues("Location").Single());
    }

    // The route list must not shadow endpoints that already exist at the root.
    [Fact]
    public async Task DoesNotShadowExistingRootRoutes()
    {
        var client = _f.CreateClient();

        Assert.Equal("ok", await client.GetStringAsync("/health"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/serverinfos")).StatusCode);
    }
}
