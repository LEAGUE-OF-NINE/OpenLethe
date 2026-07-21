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
public class RailwayHandlerTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new { userAuth = new { authCode = jwt }, parameters = p };

    private static async Task<string> NewAccount(DbWebAppFactory f, string prefix)
    {
        var name = $"{prefix}_{Guid.NewGuid():N}";
        using var scope = f.Services.CreateScope();
        await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).GetOrCreateByUsernameAsync(name);
        return scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name) + "|" + name;
    }

    [SkippableFact]
    public async Task EnterRailwayDungeon_PersistsSaveAndStartNode()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/EnterRailwayDungeon",
            Body(jwt, new { dungeonId = 5, personalities = new[] { new { pid = 100, g = 2, l = 40, es = new[] { new { id = 9, g = 1, idx = 0 } }, sp = 0, gi = 1, pord = 0 } } }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(5, result.GetProperty("saveInfo").GetProperty("id").GetInt64());
        Assert.Equal(57515885, result.GetProperty("saveInfo").GetProperty("initseed").GetInt64());
        Assert.Equal(0, result.GetProperty("startNodeData").GetProperty("nodeid").GetInt64());
        Assert.Equal(1, result.GetProperty("startNodeData").GetProperty("nodestate").GetInt64());
        Assert.Equal(10000, result.GetProperty("startNodeData").GetProperty("status")[0].GetProperty("hp").GetInt64());
        Assert.Equal(60, result.GetProperty("startNodeData").GetProperty("status")[0].GetProperty("lv").GetInt64());

        // Persisted
        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var save = AccountFields.Get<RailwaySaveInfo>(acc!.RailwaySaveInfo)!;
        Assert.Equal(5, save.id);
        Assert.Equal(100, save.personalities[0].pid);
        var nodes = AccountFields.Get<List<UpdateNodeDatas>>(acc.RailwayNodeData)!;
        Assert.Single(nodes);
        Assert.Equal(1, nodes[0].nodestate);
    }

    [SkippableFact]
    public async Task GetRailwayDungeonNodeAndLogAll_ReturnsStoredNodes_EmptyLogs()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();

        await client.PostAsJsonAsync("/api/EnterRailwayDungeon",
            Body(jwt, new { dungeonId = 5, personalities = new[] { new { pid = 1, g = 0, l = 0, es = new object[0], sp = 0, gi = 0, pord = 0 } } }));

        var resp = await client.PostAsJsonAsync("/api/GetRailwayDungeonNodeAndLogAll", Body(jwt, new { dungeonId = 5 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(1, result.GetProperty("nodeDatas").GetArrayLength());
        Assert.Equal(0, result.GetProperty("logDatas").GetArrayLength());
    }

    [SkippableFact]
    public async Task EnterRailwayDungeonNode_ReturnsPrevNodeData_NoPersist()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();

        // seed: enter creates node 0 with derived status
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon",
            Body(jwt, new { dungeonId = 5, personalities = new[] { new { pid = 7, g = 0, l = 0, es = new object[0], sp = 0, gi = 0, pord = 0 } } }));

        // entering node 1 reads prev node (id 0)
        var resp = await client.PostAsJsonAsync("/api/EnterRailwayDungeonNode", Body(jwt, new { dungeonId = 5, nodeid = 1 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(1, result.GetProperty("nodeid").GetInt64());
        Assert.Equal(7, result.GetProperty("prevStatusData")[0].GetProperty("pid").GetInt64());
        Assert.Equal(0, result.GetProperty("currentNodeId").GetInt64()); // prev node id

        // not persisted: node data still holds only node 0
        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var nodes = AccountFields.Get<List<UpdateNodeDatas>>(acc!.RailwayNodeData)!;
        Assert.Single(nodes);
    }

    [SkippableFact]
    public async Task ExitRailwayDungeonRestNode_SeedsCurrentNode_AdvancesSave()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();

        await client.PostAsJsonAsync("/api/EnterRailwayDungeon",
            Body(jwt, new { dungeonId = 5, personalities = new[] { new { pid = 1, g = 0, l = 0, es = new object[0], sp = 0, gi = 0, pord = 0 } } }));

        var resp = await client.PostAsJsonAsync("/api/ExitRailwayDungeonRestNode",
            Body(jwt, new { dungeonId = 5, nodeid = 2, personalities = new[] { new { pid = 55, g = 3, l = 0, es = new object[0], sp = 0, gi = 1, pord = 0 } } }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(2, result.GetProperty("saveInfo").GetProperty("currentnode").GetInt64());
        Assert.Equal(2, result.GetProperty("saveInfo").GetProperty("prevclearnode").GetInt64());
        Assert.Equal(2, result.GetProperty("nodeData").GetProperty("nodeid").GetInt64());
        Assert.Equal(55, result.GetProperty("nodeData").GetProperty("status")[0].GetProperty("pid").GetInt64());

        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var save = AccountFields.Get<RailwaySaveInfo>(acc!.RailwaySaveInfo)!;
        Assert.Equal(2, save.currentnode);
    }

    [SkippableFact]
    public async Task ExitRailwayDungeonNode_Win_AdvancesSave_UpsertsNode()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon",
            Body(jwt, new { dungeonId = 5, personalities = new[] { new { pid = 1, g = 0, l = 0, es = new object[0], sp = 0, gi = 0, pord = 0 } } }));

        var resp = await client.PostAsJsonAsync("/api/ExitRailwayDungeonNode",
            Body(jwt, new
            {
                dungeonId = 5, nodeid = 3, clearTurn = 4, iswin = true,
                unitStatusList = new object[0], egoSkillStockList = new object[0], statistics = new object[0],
                enemy = new { lastWave = 0, lastTurn = 0, abnoSaveDataList = new object[0] },
                buffsetbyegogift = new { nid = 3, buffs = new object[0] },
            }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(3, result.GetProperty("saveInfo").GetProperty("prevclearnode").GetInt64());
        Assert.Equal(3, result.GetProperty("nodeData").GetProperty("nodeid").GetInt64());
        Assert.Equal(1, result.GetProperty("nodeData").GetProperty("nodestate").GetInt64());

        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var buffs = AccountFields.Get<List<Buffsetsbyegogift>>(acc!.RailwayBuffs)!;
        Assert.Contains(buffs, b => b.nid == 3);
    }

    [SkippableFact]
    public async Task ExitRailwayDungeonNode_Loss_MarksNodeState_DoesNotAdvanceSave()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon",
            Body(jwt, new { dungeonId = 5, personalities = new[] { new { pid = 1, g = 0, l = 0, es = new object[0], sp = 0, gi = 0, pord = 0 } } }));

        var resp = await client.PostAsJsonAsync("/api/ExitRailwayDungeonNode",
            Body(jwt, new
            {
                dungeonId = 5, nodeid = 3, clearTurn = 4, iswin = false,
                unitStatusList = new object[0], egoSkillStockList = new object[0], statistics = new object[0],
                enemy = new { lastWave = 0, lastTurn = 0, abnoSaveDataList = new object[0] },
                buffsetbyegogift = new { nid = 3, buffs = new object[0] },
            }));
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.Equal(0, result.GetProperty("saveInfo").GetProperty("prevclearnode").GetInt64()); // not advanced
        Assert.Equal(-1, result.GetProperty("nodeData").GetProperty("nodestate").GetInt64());
    }

    [SkippableFact]
    public async Task ExitRailwayDungeon_ClearsBuffs_ReturnsSentinelLog()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon",
            Body(jwt, new { dungeonId = 5, personalities = new[] { new { pid = 1, g = 0, l = 0, es = new object[0], sp = 0, gi = 0, pord = 0 } } }));

        var resp = await client.PostAsJsonAsync("/api/ExitRailwayDungeon", Body(jwt, new { dungeonId = 5, isClear = true }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isclear").GetBoolean());
        Assert.Equal(-1, result.GetProperty("saveInfo").GetProperty("prevclearnode").GetInt64());
        Assert.Equal(-1, result.GetProperty("currentLog").GetProperty("idx").GetInt64());
        Assert.Equal(8, result.GetProperty("currentLog").GetProperty("turnspernode").GetArrayLength()); // 0..=7
        Assert.Equal(5, result.GetProperty("currentLog").GetProperty("detailstatistics").GetArrayLength()); // 0..=4
        Assert.Equal("2024-07-04T14:19:11.000Z", result.GetProperty("currentLog").GetProperty("date").GetString());

        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var buffs = AccountFields.Get<List<Buffsetsbyegogift>>(acc!.RailwayBuffs);
        Assert.True(buffs is null || buffs.Count == 0); // cleared to []
    }

    private static (string jwt, string name) Split(string s) { var p = s.Split('|'); return (p[0], p[1]); }
}
