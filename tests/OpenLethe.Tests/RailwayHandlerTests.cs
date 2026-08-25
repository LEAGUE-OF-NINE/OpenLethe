using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server;
using OpenLethe.Server.Auth;

[Collection("postgres")]
public class RailwayHandlerTests(PostgresFixture db)
{
    private const long Rr2 = 1002;   // Refraction Railway 2's rerun - the captured dungeon

    private static object Body(string jwt, object p) => new { userAuth = new { authCode = jwt }, parameters = p };

    private static async Task<string> NewAccount(DbWebAppFactory f, string prefix)
    {
        var name = $"{prefix}_{Guid.NewGuid():N}";
        using var scope = f.Services.CreateScope();
        await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).GetOrCreateByUsernameAsync(name);
        return scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name) + "|" + name;
    }

    private static (string jwt, string name) Split(string s) { var p = s.Split('|'); return (p[0], p[1]); }

    private static async Task<Dictionary<long, RailwayRun>> StateOf(DbWebAppFactory f, string name)
    {
        using var scope = f.Services.CreateScope();
        var acc = await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        return AccountFields.Get<Dictionary<long, RailwayRun>>(acc!.RailwaySaveInfo)!;
    }

    private static object OnePersonality => new[] { new { pid = 1, g = 0, l = 0, es = new object[0], sp = 0, gi = 0, pord = 0 } };

    private static object ExitNodeBody(long nodeid, long clearTurn, bool win) => new
    {
        dungeonId = Rr2, nodeid, clearTurn, iswin = win,
        unitStatusList = new[] { new { pid = 7, hp = 10000, lv = 55, g = 3, pord = 2 } },
        egoSkillStockList = new[] { new { t = "CR", n = 70 } },
        statistics = new[] { new { id = 7, gd = 100, rd = 5 } },
        enemy = new { lastWave = 0, lastTurn = 0, abnoSaveDataList = new object[0] },
        buffsetbyegogift = new { nid = nodeid, buffs = new[] { new { buffId = 1, playeregogift = 0, enemyegogift = 0 } } },
        battleStates = new object[0],
    };

    private static JsonElement Result(string json, out JsonDocument doc)
    {
        doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("result");
    }

    [SkippableFact]
    public async Task EnterRailwayDungeon_StartsRun_KeyedByDungeonId()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/EnterRailwayDungeon",
            Body(jwt, new { dungeonId = Rr2, personalities = OnePersonality }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var result = Result(await resp.Content.ReadAsStringAsync(), out var doc);
        using (doc)
        {
            var save = result.GetProperty("saveInfo");
            Assert.Equal(Rr2, save.GetProperty("id").GetInt64());
            Assert.Equal(1, save.GetProperty("payreward").GetInt64());
            Assert.Equal(-1, save.GetProperty("lastenternodeid").GetInt64());
            Assert.Equal(0, save.GetProperty("currentclearrotation").GetInt64());
            Assert.NotEqual(JsonValueKind.Null, save.GetProperty("startdate").ValueKind);
            Assert.True(save.GetProperty("initseed").GetInt64() > 0);
            Assert.Equal(save.GetProperty("initseed").GetInt64(), save.GetProperty("currentseed").GetInt64());
            Assert.Equal(0, result.GetProperty("startNodeData").GetProperty("nodeid").GetInt64());
            Assert.Equal(1, result.GetProperty("startNodeData").GetProperty("nodestate").GetInt64());
        }

        var state = await StateOf(f, name);
        Assert.Equal(Rr2, state[Rr2].save.id);
        Assert.Equal(1, state[Rr2].save.personalities[0].pid);
        Assert.Single(state[Rr2].nodes);
    }

    [SkippableFact]
    public async Task Railway_SavesAreIndependentPerDungeon()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();

        await client.PostAsJsonAsync("/api/EnterRailwayDungeon", Body(jwt, new { dungeonId = Rr2, personalities = OnePersonality }));
        await client.PostAsJsonAsync("/api/ExitRailwayDungeonNode", Body(jwt, ExitNodeBody(1, 10, true)));
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon", Body(jwt, new { dungeonId = 1001, personalities = OnePersonality }));

        var state = await StateOf(f, name);
        Assert.Equal(1, state[Rr2].save.prevclearnode);     // untouched by the 1001 run
        Assert.Equal(0, state[1001].save.prevclearnode);
        Assert.Equal(2, state[Rr2].nodes.Count);
        Assert.Single(state[1001].nodes);
    }

    [SkippableFact]
    public async Task ExitRailwayDungeonNode_Win_AdvancesSave_RerollsCurrentSeed()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon", Body(jwt, new { dungeonId = Rr2, personalities = OnePersonality }));
        var initseed = (await StateOf(f, name))[Rr2].save.initseed;

        var resp = await client.PostAsJsonAsync("/api/ExitRailwayDungeonNode", Body(jwt, ExitNodeBody(3, 12, true)));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var result = Result(await resp.Content.ReadAsStringAsync(), out var doc);
        using (doc)
        {
            var save = result.GetProperty("saveInfo");
            Assert.Equal(3, save.GetProperty("prevclearnode").GetInt64());
            Assert.Equal(3, save.GetProperty("currentnode").GetInt64());
            Assert.Equal(3, save.GetProperty("lastclearnode").GetInt64());
            Assert.Equal(-1, save.GetProperty("lastenternodeid").GetInt64());
            Assert.Equal(initseed, save.GetProperty("initseed").GetInt64());
            // the client also declares a singular nodeData; the real server omits it
            Assert.False(result.TryGetProperty("nodeData", out _));
            var node = result.GetProperty("updateNodeDatas")[0];
            Assert.Equal(3, node.GetProperty("nodeid").GetInt64());
            Assert.Equal(1, node.GetProperty("nodestate").GetInt64());
            // an all-zero enemy save echoes as {}, exactly as the capture does
            Assert.Empty(node.GetProperty("enemy").EnumerateObject());
        }

        var save2 = (await StateOf(f, name))[Rr2].save;
        Assert.Contains(save2.buffsetsbyegogift, b => b.nid == 3);   // a set carrying buffs IS stored
    }

    [SkippableFact]
    public async Task ExitRailwayDungeonNode_Loss_MarksNodeLost_DoesNotAdvanceSave()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon", Body(jwt, new { dungeonId = Rr2, personalities = OnePersonality }));

        var resp = await client.PostAsJsonAsync("/api/ExitRailwayDungeonNode", Body(jwt, ExitNodeBody(3, 12, false)));
        var result = Result(await resp.Content.ReadAsStringAsync(), out var doc);
        using (doc)
        {
            Assert.Equal(0, result.GetProperty("saveInfo").GetProperty("prevclearnode").GetInt64());
            Assert.Equal(0, result.GetProperty("saveInfo").GetProperty("lastclearnode").GetInt64());
            Assert.Equal(-1, result.GetProperty("updateNodeDatas")[0].GetProperty("nodestate").GetInt64());
        }
    }

    [SkippableFact]
    public async Task EnterRailwayDungeonNode_ResumesFromLastClearedNode_NotNodeIdMinusOne()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon", Body(jwt, new { dungeonId = Rr2, personalities = OnePersonality }));
        await client.PostAsJsonAsync("/api/ExitRailwayDungeonNode", Body(jwt, ExitNodeBody(5, 23, true)));

        // node 6 is skipped: entering 7 must still resume node 5's party
        var resp = await client.PostAsJsonAsync("/api/EnterRailwayDungeonNode", Body(jwt, new { dungeonId = Rr2, nodeid = 7 }));
        var result = Result(await resp.Content.ReadAsStringAsync(), out var doc);
        using (doc)
        {
            Assert.Equal(7, result.GetProperty("nodeid").GetInt64());
            Assert.Equal(5, result.GetProperty("prevClearNodeId").GetInt64());
            Assert.Equal(7, result.GetProperty("prevStatusData")[0].GetProperty("pid").GetInt64());
            Assert.Equal("CR", result.GetProperty("prevEgoStockData")[0].GetProperty("t").GetString());
        }

        Assert.Equal(7, (await StateOf(f, name))[Rr2].save.lastenternodeid);
    }

    [SkippableFact]
    public async Task SelectRailwayDungeonBuff_AdvancesRotation_AccumulatesBuffs()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon", Body(jwt, new { dungeonId = Rr2, personalities = OnePersonality }));
        await client.PostAsJsonAsync("/api/ExitRailwayDungeonNode", Body(jwt, ExitNodeBody(1, 10, true)));

        var resp = await client.PostAsJsonAsync("/api/SelectRailwayDungeonBuff", Body(jwt, new
        {
            dungeonId = Rr2,
            selectedBuffs = new[] { new { setId = 1, buffId = 4, targetId = 0 }, new { setId = 3, buffId = 13, targetId = 0 } },
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var result = Result(await resp.Content.ReadAsStringAsync(), out var doc);
        using (doc)
        {
            var save = result.GetProperty("saveInfo");
            Assert.Equal(1, save.GetProperty("currentclearrotation").GetInt64());
            Assert.Equal(0, save.GetProperty("currentnode").GetInt64());
            Assert.Equal(1, save.GetProperty("prevclearnode").GetInt64());
            var sets = save.GetProperty("buffsets");
            Assert.Equal(4, sets[0].GetProperty("recentbuffid").GetInt64());
            Assert.Equal(13, sets[1].GetProperty("currentbuffids")[0].GetInt64());
            // the response carries the last cleared node, so the client can rebuild the party
            Assert.Equal(1, result.GetProperty("nodeData").GetProperty("nodeid").GetInt64());
        }
    }

    [SkippableFact]
    public async Task GiveUpRailwayDungeonNodeInBattle_LeavesNodeUntouched_EchoesLogs()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon", Body(jwt, new { dungeonId = Rr2, personalities = OnePersonality }));
        await client.PostAsJsonAsync("/api/ExitRailwayDungeonNode", Body(jwt, ExitNodeBody(2, 12, true)));
        await client.PostAsJsonAsync("/api/EnterRailwayDungeonNode", Body(jwt, new { dungeonId = Rr2, nodeid = 2 }));

        var resp = await client.PostAsJsonAsync("/api/GiveUpRailwayDungeonNodeInBattle", Body(jwt, new
        {
            dungeonid = Rr2, nodeid = 2,
            abnormalityLogs = new[] { new { id = 5, k = 1 } },
        }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var result = Result(await resp.Content.ReadAsStringAsync(), out var doc);
        using (doc)
        {
            var node = result.GetProperty("nodeData");
            Assert.Equal(2, node.GetProperty("nodeid").GetInt64());
            Assert.Equal(1, node.GetProperty("nodestate").GetInt64());   // still cleared
            Assert.Equal(12, node.GetProperty("clearturn").GetInt64());
            Assert.Equal(5, result.GetProperty("abnormalityLogs")[0].GetProperty("id").GetInt64());
        }

        var state = await StateOf(f, name);
        Assert.Equal(-1, state[Rr2].save.lastenternodeid);
        Assert.Equal(2, state[Rr2].save.prevclearnode);
    }

    [SkippableFact]
    public async Task ExitRailwayDungeon_Clear_StoresLog_ResetsRun()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon", Body(jwt, new { dungeonId = Rr2, personalities = OnePersonality }));
        await client.PostAsJsonAsync("/api/ExitRailwayDungeonNode", Body(jwt, ExitNodeBody(1, 10, true)));
        await client.PostAsJsonAsync("/api/SelectRailwayDungeonBuff", Body(jwt, new
        {
            dungeonId = Rr2, selectedBuffs = new[] { new { setId = 1, buffId = 4, targetId = 0 } },
        }));
        await client.PostAsJsonAsync("/api/ExitRailwayDungeonNode", Body(jwt, ExitNodeBody(7, 5, true)));

        var resp = await client.PostAsJsonAsync("/api/ExitRailwayDungeon", Body(jwt, new { dungeonId = Rr2, isClear = true }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var result = Result(await resp.Content.ReadAsStringAsync(), out var doc);
        using (doc)
        {
            Assert.True(result.GetProperty("isclear").GetBoolean());
            var save = result.GetProperty("saveInfo");
            Assert.Equal(-1, save.GetProperty("prevclearnode").GetInt64());
            Assert.Equal(0, save.GetProperty("currentnode").GetInt64());
            Assert.Equal(7, save.GetProperty("lastclearnode").GetInt64());   // preserved
            Assert.Equal(1, save.GetProperty("clearnumber").GetInt64());
            Assert.Equal(1, save.GetProperty("lastclearrotation").GetInt64());
            Assert.Equal(0, save.GetProperty("currentclearrotation").GetInt64());
            Assert.Equal(JsonValueKind.Null, save.GetProperty("startdate").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, save.GetProperty("firstcleardate").ValueKind);
            Assert.Equal(0, save.GetProperty("initseed").GetInt64());
            Assert.Equal(0, save.GetProperty("buffsets").GetArrayLength());

            var log = result.GetProperty("currentLog");
            Assert.Equal(1, log.GetProperty("idx").GetInt64());
            Assert.Equal(15, log.GetProperty("clearturn").GetInt64());          // 10 + 5
            Assert.Equal(2, log.GetProperty("turnspernode").GetArrayLength());   // nodes 1 and 7
            Assert.Equal(1, log.GetProperty("clearrotation").GetInt64());
        }

        var state = await StateOf(f, name);
        Assert.Single(state[Rr2].logs);
        Assert.All(state[Rr2].nodes, n => Assert.Equal(0, n.nodestate));   // nodes reset, ids kept
        Assert.Equal(3, state[Rr2].nodes.Count);
    }

    [SkippableFact]
    public async Task GetRailwayDungeonNodeAndLogAll_ReturnsThatDungeonsRun()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon", Body(jwt, new { dungeonId = Rr2, personalities = OnePersonality }));
        await client.PostAsJsonAsync("/api/ExitRailwayDungeonNode", Body(jwt, ExitNodeBody(1, 10, true)));
        await client.PostAsJsonAsync("/api/ExitRailwayDungeon", Body(jwt, new { dungeonId = Rr2, isClear = true }));

        var resp = await client.PostAsJsonAsync("/api/GetRailwayDungeonNodeAndLogAll", Body(jwt, new { dungeonId = Rr2 }));
        var result = Result(await resp.Content.ReadAsStringAsync(), out var doc);
        using (doc)
        {
            Assert.Equal(Rr2, result.GetProperty("railwaySaveInfo").GetProperty("id").GetInt64());
            Assert.Equal(2, result.GetProperty("nodeDatas").GetArrayLength());
            Assert.Equal(1, result.GetProperty("logDatas").GetArrayLength());
        }

        // and a dungeon the account never played is empty, not the other one's run
        var other = await client.PostAsJsonAsync("/api/GetRailwayDungeonNodeAndLogAll", Body(jwt, new { dungeonId = 6 }));
        var otherResult = Result(await other.Content.ReadAsStringAsync(), out var doc2);
        using (doc2)
        {
            Assert.Equal(6, otherResult.GetProperty("railwaySaveInfo").GetProperty("id").GetInt64());
            Assert.Equal(0, otherResult.GetProperty("nodeDatas").GetArrayLength());
            Assert.Equal(0, otherResult.GetProperty("logDatas").GetArrayLength());
        }
    }

    [SkippableFact]
    public async Task GetRailwayDungeonSaveInfo_And_NodeDatas_And_Logs_ReadTheSameRun()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon", Body(jwt, new { dungeonId = Rr2, personalities = OnePersonality }));

        var save = await client.PostAsJsonAsync("/api/GetRailwayDungeonSaveInfo", Body(jwt, new { dungeonId = Rr2 }));
        var saveResult = Result(await save.Content.ReadAsStringAsync(), out var d1);
        using (d1) Assert.Equal(1, saveResult.GetProperty("railwaySaveInfo").GetProperty("personalities")[0].GetProperty("pid").GetInt64());

        var nodes = await client.PostAsJsonAsync("/api/GetRailwayDungeonNodeDatas", Body(jwt, new { dungeonId = Rr2 }));
        var nodesResult = Result(await nodes.Content.ReadAsStringAsync(), out var d2);
        using (d2) Assert.Equal(1, nodesResult.GetProperty("nodeDatas")[0].GetProperty("nodestate").GetInt64());

        var logs = await client.PostAsJsonAsync("/api/GetRailwayDungeonLogs", Body(jwt, new { dungeonId = Rr2 }));
        var logsResult = Result(await logs.Content.ReadAsStringAsync(), out var d3);
        using (d3) Assert.Equal(0, logsResult.GetProperty("logDatas").GetArrayLength());
    }

    [SkippableFact]
    public async Task GetRailwayDungeonSaveInfo_NoAuth_Returns401()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/GetRailwayDungeonSaveInfo", Body("not-a-real-token", new { dungeonId = Rr2 }));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [SkippableFact]
    public async Task AcquireRailwayDungeonReward_MarksUnlockedRewardsAcquired()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();

        // Railway 1001 HAS bundled static data: clearing node 2 unlocks rewards 1-2.
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon", Body(jwt, new { dungeonId = 1001, personalities = OnePersonality }));
        await client.PostAsJsonAsync("/api/ExitRailwayDungeonNode",
            Body(jwt, new { dungeonId = 1001, nodeid = 2, clearTurn = 4, iswin = true }));

        var resp = await client.PostAsJsonAsync("/api/AcquireRailwayDungeonReward", Body(jwt, new { dungeonId = 1001 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var result = Result(await resp.Content.ReadAsStringAsync(), out var doc);
        using (doc)
        {
            var states = result.GetProperty("saveInfo").GetProperty("extrarewardstate");
            Assert.Equal(2, states.GetArrayLength());
            Assert.True(states[0].GetProperty("isRewarded").GetBoolean());
            Assert.True(states[1].GetProperty("isRewarded").GetBoolean());
            Assert.True(result.GetProperty("rewardList").GetArrayLength() >= 2);
        }
    }

    [SkippableFact]
    public async Task GetRailwayDungeonExtraRewardStates_ReportsPerRequestedDungeon()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();

        await client.PostAsJsonAsync("/api/EnterRailwayDungeon", Body(jwt, new { dungeonId = 1001, personalities = OnePersonality }));
        await client.PostAsJsonAsync("/api/ExitRailwayDungeonNode",
            Body(jwt, new { dungeonId = 1001, nodeid = 3, clearTurn = 4, iswin = true }));

        var resp = await client.PostAsJsonAsync("/api/GetRailwayDungeonExtraRewardStates",
            Body(jwt, new { dungeonIds = new[] { 6, 1001 } }));
        var result = Result(await resp.Content.ReadAsStringAsync(), out var doc);
        using (doc)
        {
            var list = result.GetProperty("list");
            Assert.Equal(2, list.GetArrayLength());
            Assert.Equal(6, list[0].GetProperty("dungeonId").GetInt64());
            Assert.Equal(0, list[0].GetProperty("extraRewardState").GetArrayLength());   // never played
            Assert.Equal(1001, list[1].GetProperty("dungeonId").GetInt64());
            Assert.Equal(3, list[1].GetProperty("extraRewardState").GetArrayLength());
        }
    }

    [SkippableFact]
    public async Task GetRailwayDungeonExtraRewardStates_NullDungeonIds_ReturnsEmptyListNot500()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/GetRailwayDungeonExtraRewardStates", Body(jwt, new { dungeonIds = (object?)null }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = Result(await resp.Content.ReadAsStringAsync(), out var doc);
        using (doc) Assert.Equal(0, result.GetProperty("list").GetArrayLength());
    }

    [SkippableFact]
    public async Task ExitRailwayDungeonRestNode_SeedsCurrentNode_AdvancesSave()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = Split(await NewAccount(f, "rw"));
        var client = f.CreateClient();
        await client.PostAsJsonAsync("/api/EnterRailwayDungeon", Body(jwt, new { dungeonId = 5, personalities = OnePersonality }));

        var resp = await client.PostAsJsonAsync("/api/ExitRailwayDungeonRestNode",
            Body(jwt, new { dungeonId = 5, nodeid = 2, personalities = new[] { new { pid = 55, g = 3, l = 0, es = new object[0], sp = 0, gi = 1, pord = 0 } } }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var result = Result(await resp.Content.ReadAsStringAsync(), out var doc);
        using (doc)
        {
            Assert.Equal(2, result.GetProperty("saveInfo").GetProperty("currentnode").GetInt64());
            Assert.Equal(2, result.GetProperty("saveInfo").GetProperty("prevclearnode").GetInt64());
            Assert.Equal(55, result.GetProperty("nodeData").GetProperty("status")[0].GetProperty("pid").GetInt64());
        }

        Assert.Equal(2, (await StateOf(f, name))[5].save.currentnode);
    }
}
