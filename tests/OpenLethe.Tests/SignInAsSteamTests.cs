using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server.Auth;

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
            var acc = (await store.GetOrCreateByUsernameAsync(name))!;
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

    // An account that has logged in through Discord has Username == DiscordId, and
    // a token minted by our OAuth carries that snowflake as its subject. The dev
    // path must resolve it, not try to claim it - claiming is refused, which turned
    // every Discord user's steam login into a 400.
    [SkippableFact]
    public async Task DevLogin_ResolvesAnExistingDiscordAccount()
    {
        db.RequireDb();
        await using var factory = new DevWebAppFactory(db.ConnectionString);
        var discordId = Random.Shared.NextInt64(1, long.MaxValue).ToString();

        using (var scope = factory.Services.CreateScope())
        {
            var store = new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>());
            Assert.NotNull(await store.GetOrCreateByDiscordIdAsync(discordId));
        }

        var resp = await factory.CreateClient().PostAsJsonAsync("/login/SignInAsSteam", new
        {
            userAuth = new { uid = 0, dbid = 0, authCode = "", version = "1", synchronousDataVersion = 0 },
            parameters = new
            {
                steamToken = HexEncode(UnsignedToken(discordId)),
                version = "1",
                deviceModel = "pc",
                deviceLanguage = "en",
            },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    private sealed class DevWebAppFactory(string conn) : DbWebAppFactory(conn)
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder b)
        {
            base.ConfigureWebHost(b);
            b.UseSetting("Auth:DevAcceptAnyToken", "true");
        }
    }

    /// The dev path reads the subject WITHOUT verifying the signature, so a
    /// well-formed but unsigned token is a faithful stand-in.
    private static string UnsignedToken(string sub)
    {
        static string Part(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=')
                .Replace('+', '-').Replace('/', '_');

        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        return $"{Part("""{"alg":"HS256","typ":"JWT"}""")}.{Part($$"""{"sub":"{{sub}}","exp":{{exp}}}""")}.sig";
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
