using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server.Auth;
using Xunit;

[Collection("postgres")]
public class SignInAsSteamTests(PostgresFixture db)
{
    private static string HexEncode(string s) =>
        Convert.ToHexString(Encoding.UTF8.GetBytes(s)).ToLowerInvariant();

    [SkippableFact]
    public async Task SignInAsSteam_ReturnsAuthCode_AndIngameUid()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString);
        var client = factory.CreateClient();

        // Create an account and mint its token via the app's own services.
        var name = $"steam_{Guid.NewGuid():N}";
        int ingameId;
        string jwt;
        using (var scope = factory.Services.CreateScope())
        {
            var store = new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>());
            var acc = await store.GetOrCreateByUsernameAsync(name);
            ingameId = acc.IngameId;
            jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        }

        var body = new
        {
            userAuth = new { uid = 0, dbid = 0, authCode = "", version = "1", synchronousDataVersion = 0 },
            parameters = new { steamToken = HexEncode(jwt), version = "1", deviceModel = "pc", deviceLanguage = "en" },
        };

        var resp = await client.PostAsJsonAsync("/login/SignInAsSteam", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(jwt, result.GetProperty("userAuth").GetProperty("auth_code").GetString());
        Assert.Equal(ingameId, result.GetProperty("userAuth").GetProperty("uid").GetInt64());
    }

    [SkippableFact]
    public async Task SignInAsSteam_RejectsNonHexToken()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString);
        var client = factory.CreateClient();

        var body = new
        {
            userAuth = new { uid = 0, dbid = 0, authCode = "", version = "1", synchronousDataVersion = 0 },
            parameters = new { steamToken = "zzzz-not-hex", version = "1", deviceModel = "pc", deviceLanguage = "en" },
        };

        var resp = await client.PostAsJsonAsync("/login/SignInAsSteam", body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [SkippableFact]
    public async Task StaticLoginRoute_IsRegistered()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString);
        var client = factory.CreateClient();

        var body = new
        {
            userAuth = new { uid = 0, dbid = 0, authCode = "", version = "1", synchronousDataVersion = 0 },
            parameters = new { },
        };
        var resp = await client.PostAsJsonAsync("/login/CheckClientVersion", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
