using System.Collections.Generic;
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
public class StageAndStoryHandlerTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new
    {
        userAuth = new { authCode = jwt },
        parameters = p,
    };

    // Create an account whose chapter state contains a single node, and mint its JWT.
    private static async Task<(string name, string jwt)> SeedAsync(DbWebAppFactory f, long nodeId)
    {
        var name = $"stage_{Guid.NewGuid():N}";
        using var scope = f.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await new AccountStore(ctx).GetOrCreateByUsernameAsync(name);
        var state = new List<MainChapterState>
        {
            new() { id = 1, subcss = new() { new Subcss { id = 1, nss = new() { new Nss { id = nodeId } }, rss = new() } } },
        };
        account.ChapterState = AccountFields.Set(state);
        await ctx.SaveChangesAsync();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        return (name, jwt);
    }

    [SkippableFact]
    public async Task ExitStageBattle_Win_RegistersNode_AndReturnsUpdated()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (name, jwt) = await SeedAsync(f, 100);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/ExitStageBattle",
            Body(jwt, new { nodeid = 100, stageid = 7, iswin = true }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(7, result.GetProperty("stageid").GetInt32());
        Assert.True(result.GetProperty("iswin").GetBoolean());
        Assert.Equal(2, result.GetProperty("cleartype").GetInt32());
        var node = doc.RootElement.GetProperty("updated").GetProperty("mainChapterStateList")[0]
            .GetProperty("subcss")[0].GetProperty("nss")[0];
        Assert.Equal(2, node.GetProperty("ct").GetInt32());

        // Persisted.
        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var stored = AccountFields.Get<List<MainChapterState>>(acc!.ChapterState)!;
        Assert.Equal(2, stored[0].subcss[0].nss[0].ct);
    }

    [SkippableFact]
    public async Task ExitStageBattle_NoWin_DoesNotRegister()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (name, jwt) = await SeedAsync(f, 100);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/ExitStageBattle",
            Body(jwt, new { nodeid = 100, stageid = 7, iswin = false }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // updated.mainChapterStateList is absent (empty UpdatedFormat, null-suppressed).
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.False(doc.RootElement.GetProperty("updated").TryGetProperty("mainChapterStateList", out _));

        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var stored = AccountFields.Get<List<MainChapterState>>(acc!.ChapterState)!;
        Assert.Equal(0, stored[0].subcss[0].nss[0].ct); // unchanged
    }

    [SkippableFact]
    public async Task ExitStory_RegistersNode_AndReturnsUpdated()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (name, jwt) = await SeedAsync(f, 200);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/ExitStory",
            Body(jwt, new { nodeid = 200, stageid = 3 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var node = doc.RootElement.GetProperty("updated").GetProperty("mainChapterStateList")[0]
            .GetProperty("subcss")[0].GetProperty("nss")[0];
        Assert.Equal(2, node.GetProperty("ct").GetInt32());

        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var stored = AccountFields.Get<List<MainChapterState>>(acc!.ChapterState)!;
        Assert.Equal(2, stored[0].subcss[0].nss[0].ct);
    }
}
