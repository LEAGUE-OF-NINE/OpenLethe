using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OpenLethe.Server.Auth;
using Xunit;

// Auth:DevAcceptAnyToken - SignInAsSteam accepts ANY jwt as an identity (subject
// read without verifying the signature), auto-creates the account, and returns a
// token the server itself signed so later /api/ calls still verify.
[Collection("postgres")]
public class SignInAsSteamDevTests(PostgresFixture db)
{
    private static string HexEncode(string s) =>
        Convert.ToHexString(Encoding.UTF8.GetBytes(s)).ToLowerInvariant();

    // A structurally-valid jwt signed with a DIFFERENT secret - the server can
    // never verify it, so it only works if dev-accept-any-token is on.
    private static string ForeignJwt(string sub) =>
        new JwtService("a-totally-different-secret-not-the-server-one", TimeSpan.FromHours(1)).Mint(sub);

    private static object SteamBody(string token) => new
    {
        userAuth = new { uid = 0, dbid = 0, authCode = token, version = "1", synchronousDataVersion = 0 },
        parameters = new { steamToken = HexEncode(token), version = "1", deviceModel = "pc", deviceLanguage = "en" },
    };

    [SkippableFact]
    public async Task DevAcceptAnyToken_ForeignJwt_SignsIn_AndReturnedTokenWorksForApi()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString);
        var client = factory.WithWebHostBuilder(b => b.UseSetting("Auth:DevAcceptAnyToken", "true")).CreateClient();

        var foreign = ForeignJwt($"steamdude_{Guid.NewGuid():N}");
        var resp = await client.PostAsJsonAsync("/login/SignInAsSteam", SteamBody(foreign));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var authCode = doc.RootElement.GetProperty("result").GetProperty("userAuth").GetProperty("auth_code").GetString();
        Assert.False(string.IsNullOrEmpty(authCode));
        Assert.NotEqual(foreign, authCode); // server handed back its OWN signed token

        // The returned token verifies through the real /api/ middleware.
        var api = await client.PostAsJsonAsync("/api/LoadUserDataAll", new
        {
            userAuth = new { authCode },
            parameters = new { },
        });
        Assert.Equal(HttpStatusCode.OK, api.StatusCode);
    }

    [SkippableFact]
    public async Task WithoutDevFlag_ForeignJwt_Is400()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString);
        var client = factory.CreateClient(); // dev flag off

        var resp = await client.PostAsJsonAsync("/login/SignInAsSteam", SteamBody(ForeignJwt("nope")));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
