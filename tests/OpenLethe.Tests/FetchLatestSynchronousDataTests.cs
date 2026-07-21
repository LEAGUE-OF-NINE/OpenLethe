using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Server.Auth;
using Xunit;

// /api/FetchLatestSynchronousData hand-builds a `synchronized` payload (the
// "Welcome to Lethe" notice + a mail); the client needs this 200 to proceed past
// login. DB-free: the handler ignores the account, but the /api/ route needs a
// signature-valid token.
public class FetchLatestSynchronousDataTests : IClassFixture<FetchLatestSynchronousDataTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder b) =>
            b.UseSetting("Auth:JwtSecret", "fetch-sync-test-secret-fetch-sync-test-secret");
    }

    private readonly Factory _f;
    public FetchLatestSynchronousDataTests(Factory f) => _f = f;

    [Fact]
    public async Task ReturnsSynchronizedNoticeAndMail()
    {
        var token = _f.Services.GetRequiredService<JwtService>().Mint("test");
        var client = _f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/FetchLatestSynchronousData",
            new { userAuth = new { authCode = token }, parameters = new { } });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var sync = doc.RootElement.GetProperty("synchronized");
        Assert.Equal(513, sync.GetProperty("version").GetInt32());
        Assert.Equal("Welcome to Lethe", sync.GetProperty("noticeList")[0].GetProperty("title_EN").GetString());
        Assert.Equal("charon", sync.GetProperty("mailContentList")[0].GetProperty("senderSprName").GetString());
        // NoticeFormat.type_ must serialize as the wire name "type".
        Assert.True(sync.GetProperty("noticeList")[0].TryGetProperty("type", out _));
    }

    [Fact]
    public async Task MissingToken_Is401()
    {
        var client = _f.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/FetchLatestSynchronousData",
            new { userAuth = new { authCode = "" }, parameters = new { } });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
