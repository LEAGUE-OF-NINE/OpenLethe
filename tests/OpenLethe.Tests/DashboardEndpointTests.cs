using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server;
using OpenLethe.Server.Auth;
using OpenLethe.Server.Wire;

/// Covers the /dashboard/* port of lethe-server/server/src/dashboard.rs. These
/// routes carry the token in the request BODY (no game envelope), so they are
/// exempt from JwtAuthMiddleware and authenticate themselves.
[Collection("postgres")]
public class DashboardEndpointTests(PostgresFixture db)
{
    private async Task<(DbWebAppFactory F, HttpClient Client, string Name, string Jwt)> NewUserAsync()
    {
        var f = new DbWebAppFactory(db.ConnectionString);
        var name = $"dash_{Guid.NewGuid():N}";
        using var scope = f.Services.CreateScope();
        await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>())
            .GetOrCreateByUsernameAsync(name);
        var jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        return (f, f.CreateClient(), name, jwt);
    }

    private static async Task<Account> ReloadAsync(DbWebAppFactory f, string name)
    {
        using var scope = f.Services.CreateScope();
        return (await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>())
            .FindByUsernameAsync(name))!;
    }

    private static async Task<JsonElement> PostAsync(HttpClient c, string path, object body)
    {
        var resp = await c.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [SkippableFact]
    public async Task Egos_UpdateMergesByIdAndGetReturnsWrappedList()
    {
        db.RequireDb();
        var (f, client, name, jwt) = await NewUserAsync();
        await using var _ = f;

        // Base is the shipped default list (Rust seeds it at account creation).
        var baseline = (await PostAsync(client, "/dashboard/egos", new { token = jwt }))
            .GetProperty("list").GetArrayLength();
        Assert.True(baseline > 0);

        await PostAsync(client, "/dashboard/egos/update", new
        {
            token = jwt,
            list = new[] { new { ego_id = 1L, gacksung = 1L, acquire_time = "t1" } },
        });
        // Second call: one new id, one existing id with a changed field.
        var updated = await PostAsync(client, "/dashboard/egos/update", new
        {
            token = jwt,
            list = new[]
            {
                new { ego_id = 1L, gacksung = 4L, acquire_time = "t1" },
                new { ego_id = 2L, gacksung = 1L, acquire_time = "t2" },
            },
        });
        Assert.Equal(baseline + 2, updated.GetProperty("list").GetArrayLength());

        var got = await PostAsync(client, "/dashboard/egos", new { token = jwt });
        var egos = got.GetProperty("list").Deserialize<List<Ego>>(global::PacketJson.Options)!;
        Assert.Equal(baseline + 2, egos.Count);
        Assert.Equal(4, egos.Single(e => e.ego_id == 1).gacksung);

        var stored = AccountFields.Get<List<Ego>>((await ReloadAsync(f, name)).Egos)!;
        Assert.Equal(baseline + 2, stored.Count);
    }

    [SkippableFact]
    public async Task GetsReturnShippedDefaultsForAFreshAccount()
    {
        db.RequireDb();
        var (f, client, _, jwt) = await NewUserAsync();
        await using var _f = f;

        // Rust seeds both columns at account creation; OpenLethe fills them lazily, so
        // these routes must derive the defaults or the frontend sees an empty list.
        var egos = await PostAsync(client, "/dashboard/egos", new { token = jwt });
        Assert.True(egos.GetProperty("list").GetArrayLength() > 0);

        var personalities = await PostAsync(client, "/dashboard/personalities", new { token = jwt });
        Assert.True(personalities.GetProperty("list").GetArrayLength() > 0);
    }

    [SkippableFact]
    public async Task Personalities_UpdateMergesById()
    {
        db.RequireDb();
        var (f, client, _, jwt) = await NewUserAsync();
        await using var _f = f;

        var baseline = (await PostAsync(client, "/dashboard/personalities", new { token = jwt }))
            .GetProperty("list").GetArrayLength();
        Assert.True(baseline > 0);

        await PostAsync(client, "/dashboard/personalities/update", new
        {
            token = jwt,
            list = new[] { new { personality_id = 10101L, level = 1L } },
        });
        await PostAsync(client, "/dashboard/personalities/update", new
        {
            token = jwt,
            list = new[] { new { personality_id = 10101L, level = 45L } },
        });

        var got = await PostAsync(client, "/dashboard/personalities", new { token = jwt });
        var list = got.GetProperty("list").Deserialize<List<ResultPersonality>>(global::PacketJson.Options)!;
        // 10101 is a shipped id, so the edit merges in place rather than appending.
        Assert.Equal(baseline, list.Count);
        Assert.Equal(45, list.Single(p => p.personality_id == 10101L).level);
    }

    [SkippableFact]
    public async Task UserInfo_RoundTripsUnwrapped()
    {
        db.RequireDb();
        var (f, client, _, jwt) = await NewUserAsync();
        await using var _f = f;

        var updated = await PostAsync(client, "/dashboard/userinfo/update", new
        {
            token = jwt,
            userInfo = new { uid = 7L, level = 3L, exp = 900L, stamina = 20L },
        });
        Assert.Equal(7, updated.GetProperty("uid").GetInt64());

        var got = await PostAsync(client, "/dashboard/userinfo", new { token = jwt });
        Assert.Equal(3, got.GetProperty("level").GetInt64());
    }

    [SkippableFact]
    public async Task StoryDungeonAndStoryMd_RoundTripUnderDataField()
    {
        db.RequireDb();
        var (f, client, _, jwt) = await NewUserAsync();
        await using var _f = f;

        await PostAsync(client, "/dashboard/storydungeon/update",
            new { token = jwt, data = new { dungeonid = 11L } });
        var story = await PostAsync(client, "/dashboard/storydungeon/get", new { token = jwt });
        Assert.Equal(11, story.GetProperty("dungeonid").GetInt64());

        await PostAsync(client, "/dashboard/storymirrordungeon/update",
            new { token = jwt, data = new { dungeonid = 22L } });
        var smd = await PostAsync(client, "/dashboard/storymirrordungeon/get", new { token = jwt });
        Assert.Equal(22, smd.GetProperty("dungeonid").GetInt64());
    }

    [SkippableFact]
    public async Task MdReset_ClearsEveryDungeonColumnToSentinels()
    {
        db.RequireDb();
        var (f, client, name, jwt) = await NewUserAsync();
        await using var _f = f;

        await PostAsync(client, "/dashboard/md/update",
            new { token = jwt, saveInfo = new { dungeonId = 5L, idx = 2L } });
        await PostAsync(client, "/dashboard/storydungeon/update",
            new { token = jwt, data = new { dungeonid = 11L } });
        var beforeReset = await PostAsync(client, "/dashboard/md/get", new { token = jwt });
        Assert.Equal(5, beforeReset.GetProperty("dungeonId").GetInt64());

        var resp = await client.PostAsJsonAsync("/dashboard/md/reset", new { token = jwt });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var md = await PostAsync(client, "/dashboard/md/get", new { token = jwt });
        Assert.Equal(-1, md.GetProperty("dungeonId").GetInt64());
        Assert.Equal(-1, md.GetProperty("idx").GetInt64());
        Assert.Equal(-1, md.GetProperty("currentInfo").GetProperty("eid").GetInt64());

        var story = await PostAsync(client, "/dashboard/storydungeon/get", new { token = jwt });
        Assert.Equal(0, story.GetProperty("dungeonid").GetInt64());
        var smd = await PostAsync(client, "/dashboard/storymirrordungeon/get", new { token = jwt });
        Assert.Equal(-1, smd.GetProperty("dungeonid").GetInt64());

        Assert.Equal("{}", (await ReloadAsync(f, name)).RailwaySaveInfo);
    }

    [SkippableFact]
    public async Task IngameId_RoundTrips()
    {
        db.RequireDb();
        var (f, client, name, jwt) = await NewUserAsync();
        await using var _f = f;

        var updated = await PostAsync(client, "/dashboard/ingameid/update", new { token = jwt, ingameId = 4242 });
        Assert.Equal(4242, updated.GetProperty("ingameId").GetInt32());

        var got = await PostAsync(client, "/dashboard/ingameid", new { token = jwt });
        Assert.Equal(4242, got.GetProperty("ingameId").GetInt32());
        Assert.Equal(4242, (await ReloadAsync(f, name)).IngameId);
    }

    [SkippableFact]
    public async Task ChapterState_ResetIsUnclearedAndCompleteIsCleared()
    {
        db.RequireDb();
        var (f, client, name, jwt) = await NewUserAsync();
        await using var _f = f;

        await client.PostAsJsonAsync("/dashboard/chapterstate/complete", new { token = jwt });
        var done = AccountFields.Get<List<MainChapterState>>((await ReloadAsync(f, name)).ChapterState)!;
        Assert.NotEmpty(done);
        Assert.All(done.SelectMany(c => c.subcss).SelectMany(s => s.nss), n => Assert.Equal(2, n.ct));

        await client.PostAsJsonAsync("/dashboard/chapterstate/reset", new { token = jwt });
        var fresh = AccountFields.Get<List<MainChapterState>>((await ReloadAsync(f, name)).ChapterState)!;
        Assert.Equal(done.Count, fresh.Count);
        Assert.All(fresh.SelectMany(c => c.subcss).SelectMany(s => s.nss), n =>
        {
            Assert.Equal(0, n.ct);
            Assert.Equal(0, n.cn);
        });
    }

    [SkippableFact]
    public async Task PersonalitiesLocalize_ReturnsIdentitiesSortedWithSinnerId()
    {
        db.RequireDb();
        var (f, client, _, jwt) = await NewUserAsync();
        await using var _f = f;

        var resp = await client.PostAsJsonAsync("/dashboard/personalities/localize", new { token = jwt });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var list = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;

        Assert.True(list.GetArrayLength() > 100);
        var ids = list.EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToList();
        Assert.Equal(ids.OrderBy(x => x), ids);

        var yiSang = list.EnumerateArray().First(x => x.GetProperty("id").GetInt64() == 10101);
        Assert.Equal("Yi Sang", yiSang.GetProperty("name").GetString());
        Assert.Equal(1, yiSang.GetProperty("sinner_id").GetInt64());
        Assert.False(yiSang.GetProperty("custom").GetBoolean());
    }

    [SkippableFact]
    public async Task EphemeralToken_IsMintedOnceAndCannotRefreshItself()
    {
        db.RequireDb();
        var (f, client, _, jwt) = await NewUserAsync();
        await using var _f = f;

        var minted = await PostAsync(client, "/dashboard/auth/token", new { token = jwt });
        var ephemeral = minted.GetProperty("token").GetString()!;
        Assert.NotEqual(jwt, ephemeral);

        // The ephemeral token still authenticates ordinary dashboard reads...
        await PostAsync(client, "/dashboard/userinfo", new { token = ephemeral });

        // ...but cannot mint another one.
        var refused = await client.PostAsJsonAsync("/dashboard/auth/token", new { token = ephemeral });
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [SkippableFact]
    public async Task BadTokenIs401_AndMalformedBodyIs400()
    {
        db.RequireDb();
        var (f, client, _, _) = await NewUserAsync();
        await using var _f = f;

        var bad = await client.PostAsJsonAsync("/dashboard/egos", new { token = "not.a.jwt" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);

        var malformed = await client.PostAsync("/dashboard/egos",
            new StringContent("{", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [SkippableFact]
    public async Task ServerInfos_IsServedWithoutAuth()
    {
        db.RequireDb();
        var (f, client, _, _) = await NewUserAsync();
        await using var _f = f;

        var resp = await client.GetAsync("/serverinfos");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var list = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("windows", list[0].GetProperty("platform").GetString());
        Assert.True(list[0].GetProperty("enablePacketCrypt").GetBoolean());
    }
}
