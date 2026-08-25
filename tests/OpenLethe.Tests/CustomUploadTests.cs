using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server;
using OpenLethe.Server.Auth;
using OpenLethe.Server.Wire;

/// Covers the /custom/upload/* port of lethe-server/server/src/custom/mod.rs.
[Collection("postgres")]
public class CustomUploadTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new
    {
        userAuth = new { authCode = jwt },
        parameters = p,
    };

    private async Task<(DbWebAppFactory F, HttpClient Client, string Name, string Jwt)> NewUserAsync()
    {
        var f = new DbWebAppFactory(db.ConnectionString);
        var name = $"custom_{Guid.NewGuid():N}";
        using var scope = f.Services.CreateScope();
        await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>())
            .GetOrCreateByUsernameAsync(name);
        return (f, f.CreateClient(), name, scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name));
    }

    private static async Task<Account> ReloadAsync(DbWebAppFactory f, string name)
    {
        using var scope = f.Services.CreateScope();
        return (await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>())
            .FindByUsernameAsync(name))!;
    }

    [SkippableFact]
    public async Task ThemeFloorUpload_FlattensNestedListsAndReplaces()
    {
        db.RequireDb();
        var (f, client, name, jwt) = await NewUserAsync();
        await using var _ = f;

        var resp = await client.PostAsJsonAsync("/custom/upload/mirrordungeon-theme-floor", Body(jwt, new
        {
            list = new object[]
            {
                new { list = new object[] { new { id = 900001L, egoGiftPool = new[] { 1L, 2L } } } },
                new { list = new object[] { new { id = 900002L } } },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var stored = AccountFields.Get<List<ThemeStatic>>((await ReloadAsync(f, name)).CustomTheme)!;
        Assert.Equal(new[] { 900001L, 900002L }, stored.Select(t => t.id));
        Assert.Equal(new[] { 1L, 2L }, stored[0].egoGiftPool);

        // A second upload replaces the column wholesale (Rust never merges).
        await client.PostAsJsonAsync("/custom/upload/mirrordungeon-theme-floor", Body(jwt, new
        {
            list = new object[] { new { list = new object[] { new { id = 900003L } } } },
        }));
        stored = AccountFields.Get<List<ThemeStatic>>((await ReloadAsync(f, name)).CustomTheme)!;
        Assert.Equal(new[] { 900003L }, stored.Select(t => t.id));
    }

    [SkippableFact]
    public async Task PersonalityUpload_StoresIdentitiesAndTheyBecomeOwnedPersonalities()
    {
        db.RequireDb();
        var (f, client, name, jwt) = await NewUserAsync();
        await using var _ = f;

        var resp = await client.PostAsJsonAsync("/custom/upload/personality", Body(jwt, new
        {
            list = new object[]
            {
                new { list = new object[] { new { id = 90001L, characterId = 1L } } },
                new { list = new object[] { new { id = 90002L, characterId = 2L } } },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var account = await ReloadAsync(f, name);
        var stored = AccountFields.Get<List<CustomIdentity>>(account.CustomIdentities)!;
        Assert.Equal(new[] { 90001L, 90002L }, stored.Select(i => i.id));

        // The uploaded ids reach the client as owned personalities at default stats.
        var load = await client.PostAsJsonAsync("/api/LoadUserDataAll", Body(jwt, new { }));
        var list = JsonDocument.Parse(await load.Content.ReadAsStringAsync())
            .RootElement.GetProperty("updated").GetProperty("personalityList")
            .Deserialize<List<ResultPersonality>>(global::PacketJson.Options)!;
        var custom = list.Single(p => p.personality_id == 90001L);
        Assert.Equal(60, custom.level);
        Assert.Equal(4, custom.gacksung);
        Assert.True(list.Count > 2); // vanilla ids survive
    }

    [SkippableFact]
    public async Task Upload_WithoutValidToken_Is401()
    {
        db.RequireDb();
        var (f, client, _, _) = await NewUserAsync();
        await using var _f = f;

        var resp = await client.PostAsJsonAsync("/custom/upload/personality",
            Body("not-a-jwt", new { list = Array.Empty<object>() }));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
