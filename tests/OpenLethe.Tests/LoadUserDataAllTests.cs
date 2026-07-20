using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server.Auth;
using Xunit;

[Collection("postgres")]
public class LoadUserDataAllTests(PostgresFixture db)
{
    private static object Body(string jwt) => new
    {
        userAuth = new { uid = 0, dbid = 0, authCode = jwt, version = "1", synchronousDataVersion = 0 },
        parameters = new { },
    };

    private async Task<(HttpClient client, string jwt)> NewUserAsync(DbWebAppFactory factory)
    {
        var name = $"load_{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var store = new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        await store.GetOrCreateByUsernameAsync(name);
        var jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        return (factory.CreateClient(), jwt);
    }

    [SkippableFact]
    public async Task LoadUserDataAll_ReturnsDefaults_AndUpdatedPayload()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString);
        var (client, jwt) = await NewUserAsync(factory);

        var resp = await client.PostAsJsonAsync("/api/LoadUserDataAll", Body(jwt));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var result = root.GetProperty("result");
        Assert.Equal(3, result.GetProperty("showedWeekByMinistory").GetInt32());
        Assert.Equal("Private Account", result.GetProperty("profile").GetProperty("public_uid").GetString());
        Assert.Equal(200, result.GetProperty("profile").GetProperty("level").GetInt32());
        Assert.Equal(-1, result.GetProperty("dailyLoginId").GetInt32());

        // banners: 5 entries, first is id 35, all value -1, idx 0..4.
        var banners = result.GetProperty("profile").GetProperty("banners");
        Assert.Equal(5, banners.GetArrayLength());
        Assert.Equal(35, banners[0].GetProperty("id").GetInt32());
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(i, banners[i].GetProperty("idx").GetInt32());
            var id = banners[i].GetProperty("id").GetInt32();
            Assert.InRange(id, 1, 43 + (i == 0 ? 2 : 0)); // first is fixed 35
            Assert.Equal(-1, banners[i].GetProperty("value").GetInt32());
        }

        var updated = root.GetProperty("updated");
        Assert.True(updated.GetProperty("isInitialized").GetBoolean());
        Assert.Equal(1234, updated.GetProperty("userInfo").GetProperty("uid").GetInt32());
        Assert.Equal(99999, updated.GetProperty("userInfo").GetProperty("stamina").GetInt32());
        Assert.True(updated.GetProperty("egoList").GetArrayLength() > 0);
        Assert.True(updated.GetProperty("personalityList").GetArrayLength() > 0);
        Assert.True(updated.GetProperty("mainChapterStateList").GetArrayLength() > 0);
        Assert.Equal(100, updated.GetProperty("announcer").GetProperty("announcer_ids").GetArrayLength());
        Assert.Equal(2, updated.GetProperty("danteAbilityList").GetArrayLength());
        Assert.Equal(2325, updated.GetProperty("danteAbilityList")[0].GetProperty("remaincount").GetInt32());

        // itemList: Enkap modules + Lunacy.
        var items = updated.GetProperty("itemList");
        Assert.Equal(11, items[0].GetProperty("item_id").GetInt32());
        Assert.Equal(99999, items[0].GetProperty("num").GetInt32());

        // battlePass flags.
        var bp = updated.GetProperty("battlePass");
        Assert.True(bp.GetProperty("is_limbus").GetBoolean());
        Assert.Equal(12, bp.GetProperty("today_rand_value").GetInt32());
        Assert.Equal(4, bp.GetProperty("limbus_apply_level").GetInt32());

        // Omitted UpdatedFormat fields are absent (per-field null suppression).
        Assert.False(updated.TryGetProperty("gachaList", out _));
        Assert.False(updated.TryGetProperty("missionList", out _));
    }

    [SkippableFact]
    public async Task LoadUserDataAll_PersistsLazyFilledEgos()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString);
        var (client, jwt) = await NewUserAsync(factory);

        var first = await client.PostAsJsonAsync("/api/LoadUserDataAll", Body(jwt));
        using var d1 = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var count1 = d1.RootElement.GetProperty("updated").GetProperty("egoList").GetArrayLength();

        var second = await client.PostAsJsonAsync("/api/LoadUserDataAll", Body(jwt));
        using var d2 = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        var count2 = d2.RootElement.GetProperty("updated").GetProperty("egoList").GetArrayLength();

        Assert.True(count1 > 0);
        Assert.Equal(count1, count2); // second read comes from the persisted column
    }

    [SkippableFact]
    public async Task LoadUserDataAll_MissingToken_Is401()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString);
        var client = factory.CreateClient();

        var body = new
        {
            userAuth = new { uid = 0, dbid = 0, authCode = "", version = "1", synchronousDataVersion = 0 },
            parameters = new { },
        };
        var resp = await client.PostAsJsonAsync("/api/LoadUserDataAll", body);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
