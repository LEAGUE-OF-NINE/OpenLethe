using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class PathSanitizerTests : IClassFixture<PathSanitizerTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder b) =>
            b.UseSetting("Auth:JwtSecret", "path-sanitizer-test-secret-path-sanitizer-test-secret");
    }

    private readonly Factory _factory;
    private readonly string _token;

    public PathSanitizerTests(Factory factory)
    {
        _factory = factory;
        _token = factory.Services.GetRequiredService<OpenLethe.Server.Auth.JwtService>().Mint("test");
    }

    [Theory]
    [InlineData("//api/AcquireAttendanceReward")]
    [InlineData("/api//AcquireAttendanceReward")]
    [InlineData("///api///AcquireAttendanceReward")]
    public async Task DuplicateSlashes_AreCollapsed(string route)
    {
        var client = _factory.CreateClient();

        // Build an absolute URI by string concatenation rather than passing `route`
        // straight to PostAsJsonAsync. HttpClient resolves a *relative* request URI
        // against BaseAddress using RFC 3986 reference-resolution, under which a
        // leading "//" is a network-path reference: it replaces the authority, so
        // "//api/..." becomes host "api" with path "/..." (and a leading "///" is
        // an invalid host and throws) - the "api" segment never reaches the server
        // as part of the path, so no server-side sanitizer could ever fix it. A raw
        // HTTP client (the actual Limbus client) has no such client-side merge step;
        // it puts the literal duplicated slashes on the wire. Concatenating into an
        // absolute URI string reproduces that: single-arg `Uri` parsing keeps the
        // path exactly as written instead of merging it against a base.
        var requestUri = new Uri(client.BaseAddress!.GetLeftPart(UriPartial.Authority) + route);

        var resp = await client.PostAsJsonAsync(requestUri, new
        {
            userAuth = new { uid = 1, dbid = 1, authCode = _token, version = "1", synchronousDataVersion = 0 },
            parameters = new { },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
