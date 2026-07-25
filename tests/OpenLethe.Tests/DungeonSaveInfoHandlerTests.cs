using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server.Auth;
using Xunit;

[Collection("postgres")]
public class DungeonSaveInfoHandlerTests(PostgresFixture db)
{
    [SkippableFact]
    public async Task GetDungeonSaveInfoAll_FreshAccount_ReturnsSentinelBlob()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var name = $"dsi_{Guid.NewGuid():N}";
        string jwt;
        using (var scope = f.Services.CreateScope())
        {
            await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).GetOrCreateByUsernameAsync(name);
            jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        }
        var client = f.CreateClient();

        var body = new { userAuth = new { authCode = jwt }, parameters = new { } };
        var resp = await client.PostAsJsonAsync("/api/GetDungeonSaveInfoAll", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(-1, result.GetProperty("mirrorOriginSaveInfo").GetProperty("dungeonId").GetInt32());
        Assert.Equal(-1, result.GetProperty("mirrorOriginSaveInfo").GetProperty("currentInfo").GetProperty("eid").GetInt32());
        Assert.Equal(-1, result.GetProperty("storyMirrorSaveInfo").GetProperty("dungeonid").GetInt32());

        var clears = result.GetProperty("mirrorDungeonClearInfos");
        Assert.Equal(2, clears.GetArrayLength());
        Assert.Equal(9999, clears[0].GetProperty("clearnumber").GetInt32());
        Assert.Equal(4, clears[0].GetProperty("dungeonid").GetInt32());
        Assert.Equal(1, clears[1].GetProperty("idx").GetInt32());
    }
}
