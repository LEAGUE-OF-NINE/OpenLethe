using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server.Auth;

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
        // One entry per boss-raid in static data, all idle.
        Assert.Equal(4, states.GetArrayLength());
        Assert.Equal(10001, states[0].GetProperty("raidId").GetInt32());
        Assert.All(states.EnumerateArray(), s => Assert.Equal(0, s.GetProperty("state").GetInt32()));
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

    [SkippableFact]
    public async Task Exit_Win_FinishesRun_EndPaysOutAndClears()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (client, jwt) = await NewUserAsync(f);

        await client.PostAsJsonAsync("/api/EnterBossRaid", Body(jwt, new { raidId = 10004, difficulty = 2 }));
        await StartAsync(client, jwt, 10004, 10101);
        var exit = await client.PostAsJsonAsync("/api/ExitBossRaidBattle", Body(jwt, new { raidId = 10004, isWin = true, clearTurn = 3 }));
        Assert.Equal(HttpStatusCode.OK, exit.StatusCode);
        using (var doc = JsonDocument.Parse(await exit.Content.ReadAsStringAsync()))
        {
            // Real server: the save survives the win as state 3 / isClear until EndBossRaid.
            var save = doc.RootElement.GetProperty("result").GetProperty("saveInfo");
            Assert.Equal(3, save.GetProperty("state").GetInt32());
            Assert.True(save.GetProperty("isClear").GetBoolean());
            Assert.Equal(1, save.GetProperty("currentIdx").GetInt32());
        }

        var end = await client.PostAsJsonAsync("/api/EndBossRaid", Body(jwt, new { raidId = 10004, reset = false }));
        using (var doc = JsonDocument.Parse(await end.Content.ReadAsStringAsync()))
        {
            var result = doc.RootElement.GetProperty("result");
            var reward = result.GetProperty("rewards")[0];
            Assert.Equal("UNLOCK_CODE", reward.GetProperty("type").GetString());
            Assert.Equal(912723, reward.GetProperty("id").GetInt32()); // 10004 HARD
            Assert.Equal(1, result.GetProperty("logData").GetProperty("partydetails").GetArrayLength());
        }

        var get = await client.PostAsJsonAsync("/api/GetBossRaidSaveInfo", Body(jwt, new { raidId = 10004 }));
        using var d2 = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, d2.RootElement.GetProperty("result").GetProperty("saveInfo").ValueKind);
    }

    private async Task<HttpResponseMessage> StartAsync(HttpClient client, string jwt, int raidId, params int[] pids) =>
        await client.PostAsJsonAsync("/api/StartBossRaidBattle", Body(jwt, new
        {
            raidId,
            personalities = pids.Select((pid, i) => new { pid, pord = i + 1 }).ToArray(),
        }));

    private async Task<JsonElement> LoseAsync(HttpClient client, string jwt, int raidId, object[]? dante = null)
    {
        var exit = await client.PostAsJsonAsync("/api/ExitBossRaidBattle", Body(jwt, new
        {
            raidId, isWin = false, clearTurn = 12, danteAbilityUsageLogs = dante ?? Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.OK, exit.StatusCode);
        using var doc = JsonDocument.Parse(await exit.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    [SkippableFact]
    public async Task UsedTeams_StayBlocked_ForTheWholeRun()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (client, jwt) = await NewUserAsync(f);

        await client.PostAsJsonAsync("/api/EnterBossRaid", Body(jwt, new { raidId = 10004, difficulty = 2 }));
        await StartAsync(client, jwt, 10004, 10101, 10216);
        await LoseAsync(client, jwt, 10004);
        await StartAsync(client, jwt, 10004, 10114, 10212);
        var root = await LoseAsync(client, jwt, 10004);

        // Both earlier teams are in saveInfo.personalities, not only the previous one.
        var used = root.GetProperty("result").GetProperty("saveInfo").GetProperty("personalities")
            .EnumerateArray().Select(e => e.GetInt32()).ToList();
        Assert.Equal(new[] { 10101, 10216, 10114, 10212 }, used);

        // Team 1's identities are still refused on try 3.
        var reuse = await StartAsync(client, jwt, 10004, 10101);
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
    }

    [SkippableFact]
    public async Task Run_EndsAfterMaxPartyCount()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (client, jwt) = await NewUserAsync(f);

        await client.PostAsJsonAsync("/api/EnterBossRaid", Body(jwt, new { raidId = 10004, difficulty = 2 }));
        JsonElement root = default;
        for (var i = 0; i < 3; i++) // maxPartyCount = 3
        {
            Assert.Equal(HttpStatusCode.OK, (await StartAsync(client, jwt, 10004, 10101 + i * 100)).StatusCode);
            root = await LoseAsync(client, jwt, 10004);
        }
        var save = root.GetProperty("result").GetProperty("saveInfo");
        Assert.Equal(3, save.GetProperty("currentIdx").GetInt32());
        Assert.Equal(3, save.GetProperty("state").GetInt32());
        Assert.False(save.GetProperty("isClear").GetBoolean());

        // No fourth try.
        Assert.Equal(HttpStatusCode.BadRequest, (await StartAsync(client, jwt, 10004, 10401)).StatusCode);
    }

    [SkippableFact]
    public async Task Durantes_SentOnEnter_AndSpentAcrossTries()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (client, jwt) = await NewUserAsync(f);

        var enter = await client.PostAsJsonAsync("/api/EnterBossRaid", Body(jwt, new { raidId = 10004, difficulty = 2 }));
        using (var doc = JsonDocument.Parse(await enter.Content.ReadAsStringAsync()))
        {
            var pool = doc.RootElement.GetProperty("updated").GetProperty("danteAbilityList")[0];
            Assert.Equal(6, pool.GetProperty("category").GetInt32()); // 10004's danteAbilityCategory
            Assert.Equal(2, pool.GetProperty("remaincount").GetInt32());
            Assert.Equal(new[] { 90101, 80101, 70101, 60101 },
                pool.GetProperty("abilityids").EnumerateArray().Select(e => e.GetInt32()));
        }

        await StartAsync(client, jwt, 10004, 10101);
        var root = await LoseAsync(client, jwt, 10004, new object[] { new { abilityId = 70101, count = 1 } });
        var after = root.GetProperty("updated").GetProperty("danteAbilityList")[0];
        Assert.Equal(1, after.GetProperty("remaincount").GetInt32());
        Assert.Equal(70101, after.GetProperty("usedlogs")[0].GetProperty("abilityId").GetInt32());

        await StartAsync(client, jwt, 10004, 10201);
        root = await LoseAsync(client, jwt, 10004, new object[] { new { abilityId = 60101, count = 1 } });
        after = root.GetProperty("updated").GetProperty("danteAbilityList")[0];
        Assert.Equal(0, after.GetProperty("remaincount").GetInt32());
        Assert.Equal(2, after.GetProperty("usedlogs").GetArrayLength());
    }

    [SkippableFact]
    public async Task Exit_Loss_AdvancesCurrentIdx()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (client, jwt) = await NewUserAsync(f);

        await client.PostAsJsonAsync("/api/EnterBossRaid", Body(jwt, new { raidId = 10001, difficulty = 1 }));
        var exit = await client.PostAsJsonAsync("/api/ExitBossRaidBattle", Body(jwt, new { raidId = 10001, isWin = false, clearTurn = 0 }));
        using var doc = JsonDocument.Parse(await exit.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetProperty("result").GetProperty("saveInfo").GetProperty("currentIdx").GetInt32());
    }

    [SkippableFact]
    public async Task StartBattle_AfterEnter_ReturnsEnemyAndEgostocks()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (client, jwt) = await NewUserAsync(f);

        await client.PostAsJsonAsync("/api/EnterBossRaid", Body(jwt, new { raidId = 10001, difficulty = 1 }));
        var start = await client.PostAsJsonAsync("/api/StartBossRaidBattle", Body(jwt, new
        {
            raidId = 10001,
            personalities = new[] { new { pid = 42, pord = 0 } },
        }));
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        using var doc = JsonDocument.Parse(await start.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.True(result.TryGetProperty("enemy", out _));
        Assert.True(result.TryGetProperty("egostocks", out _));
    }

    [SkippableFact]
    public async Task StartBattle_NoActiveSave_BadRequest()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (client, jwt) = await NewUserAsync(f);

        var start = await client.PostAsJsonAsync("/api/StartBossRaidBattle", Body(jwt, new
        {
            raidId = 10001,
            personalities = Array.Empty<object>(),
        }));
        Assert.Equal(HttpStatusCode.BadRequest, start.StatusCode);
    }
}
