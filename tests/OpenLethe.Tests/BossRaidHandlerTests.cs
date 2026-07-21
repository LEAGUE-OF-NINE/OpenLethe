using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server.Auth;
using Xunit;

[Collection("postgres")]
public class BossRaidHandlerTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new
    {
        userAuth = new { uid = 0, dbid = 0, authCode = jwt, version = "1", synchronousDataVersion = 0 },
        parameters = p,
    };

    private async Task<(HttpClient client, string jwt)> NewUserAsync(DbWebAppFactory f)
    {
        var name = $"raid_{Guid.NewGuid():N}";
        using var scope = f.Services.CreateScope();
        await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).GetOrCreateByUsernameAsync(name);
        var jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        return (f.CreateClient(), jwt);
    }

    [SkippableFact]
    public async Task GetStates_FreshAccount_State0()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (client, jwt) = await NewUserAsync(f);

        var resp = await client.PostAsJsonAsync("/api/GetBossRaidStates", Body(jwt, new { }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var states = doc.RootElement.GetProperty("result").GetProperty("raidStates");
        Assert.Equal(1, states.GetArrayLength());
        Assert.Equal(10001, states[0].GetProperty("raidId").GetInt32());
        Assert.Equal(0, states[0].GetProperty("state").GetInt32());
    }

    [SkippableFact]
    public async Task GetSaveInfo_FreshAccount_NullSave()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (client, jwt) = await NewUserAsync(f);

        var resp = await client.PostAsJsonAsync("/api/GetBossRaidSaveInfo", Body(jwt, new { raidId = 10001 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var save = doc.RootElement.GetProperty("result").GetProperty("saveInfo");
        Assert.Equal(JsonValueKind.Null, save.ValueKind); // neutral "{}" default -> null
    }

    [SkippableFact]
    public async Task Enter_PersistsSave_ThenGetStatesReflectsState2()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (client, jwt) = await NewUserAsync(f);

        var enter = await client.PostAsJsonAsync("/api/EnterBossRaid", Body(jwt, new { raidId = 10001, difficulty = 3 }));
        Assert.Equal(HttpStatusCode.OK, enter.StatusCode);
        using (var doc = JsonDocument.Parse(await enter.Content.ReadAsStringAsync()))
        {
            var save = doc.RootElement.GetProperty("result").GetProperty("saveInfo");
            Assert.Equal(10001, save.GetProperty("raidId").GetInt32());
            Assert.Equal(3, save.GetProperty("difficulty").GetInt32());
            Assert.Equal(2, save.GetProperty("state").GetInt32());
        }

        // Persisted: GetBossRaidStates now reports state 2.
        var states = await client.PostAsJsonAsync("/api/GetBossRaidStates", Body(jwt, new { }));
        using var d2 = JsonDocument.Parse(await states.Content.ReadAsStringAsync());
        Assert.Equal(2, d2.RootElement.GetProperty("result").GetProperty("raidStates")[0].GetProperty("state").GetInt32());
    }

    [SkippableFact]
    public async Task End_WithReset_ClearsSave()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (client, jwt) = await NewUserAsync(f);

        await client.PostAsJsonAsync("/api/EnterBossRaid", Body(jwt, new { raidId = 10001, difficulty = 1 }));
        var end = await client.PostAsJsonAsync("/api/EndBossRaid", Body(jwt, new { raidId = 10001, reset = true }));
        Assert.Equal(HttpStatusCode.OK, end.StatusCode);

        // Save cleared -> GetSaveInfo null again.
        var get = await client.PostAsJsonAsync("/api/GetBossRaidSaveInfo", Body(jwt, new { raidId = 10001 }));
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("result").GetProperty("saveInfo").ValueKind);
    }
}
