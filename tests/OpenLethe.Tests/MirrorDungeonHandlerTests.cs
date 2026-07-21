using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server;
using OpenLethe.Server.Auth;
using OpenLethe.Server.Wire;
using Xunit;

[Collection("postgres")]
public class MirrorDungeonHandlerTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new { userAuth = new { authCode = jwt }, parameters = p };

    private static async Task<(string jwt, string name)> NewAccount(DbWebAppFactory f)
    {
        var name = $"md_{Guid.NewGuid():N}";
        using var scope = f.Services.CreateScope();
        await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).GetOrCreateByUsernameAsync(name);
        var jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        return (jwt, name);
    }

    [SkippableFact]
    public async Task EnterMirrorDungeon_PersistsFixedSave()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccount(f);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 1 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        var save = result.GetProperty("saveInfo");
        Assert.Equal(4, save.GetProperty("dungeonId").GetInt64());
        Assert.Equal(2, save.GetProperty("version").GetInt64());
        var ci = save.GetProperty("currentInfo");
        Assert.Equal(500, ci.GetProperty("cost").GetInt64());
        Assert.Equal("None", ci.GetProperty("startKeyword").GetString());
        Assert.Equal(10, ci.GetProperty("seps").GetArrayLength());
        Assert.Equal("Combustion", ci.GetProperty("seps")[0].GetProperty("keyword").GetString());
        Assert.Equal(7, ci.GetProperty("ess").GetArrayLength());
        Assert.Equal(-1, ci.GetProperty("cfs")[0].GetProperty("floor").GetInt64());
        Assert.Equal(15, save.GetProperty("encounterstatistics").GetArrayLength());
        Assert.Equal(0, result.GetProperty("recentCharacterList").GetArrayLength());

        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var stored = AccountFields.Get<MirrorOriginSaveInfo>(acc!.MdSaveInfo)!;
        Assert.Equal(4, stored.dungeonId);
        Assert.Equal(500, stored.currentInfo.cost);
    }

    [SkippableFact]
    public async Task ReEnterMirrorDungeon_EchoesStoredSave()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccount(f);
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterMirrorDungeon", Body(jwt, new { dungeonid = 4, idx = 0 }));

        var resp = await client.PostAsJsonAsync("/api/ReEnterMirrorDungeon", Body(jwt, new { }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var save = doc.RootElement.GetProperty("result").GetProperty("saveInfo");
        Assert.Equal(4, save.GetProperty("dungeonId").GetInt64());
        Assert.Equal(500, save.GetProperty("currentInfo").GetProperty("cost").GetInt64());
    }
}
