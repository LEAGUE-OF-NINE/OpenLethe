using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenLethe.Server.Wire;
using Xunit;

public class RailwayWireTests
{
    [Fact]
    public void RailwaySaveInfo_SerializesWireFieldNames_ListsAsEmptyArrays_DatesAsNull()
    {
        var save = new RailwaySaveInfo
        {
            id = 1002,
            personalities = new List<Personalities> { new() { pid = 1, es = new List<Egos> { new() { id = 9, g = 1, idx = 0 } } } },
            buffsets = new List<Buffsets> { new() { setid = 3, currentbuffids = new List<long> { 13 } } },
            clearnumber = 2,
            initseed = 8247907,
            currentseed = 8247907,
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(save, global::PacketJson.Options));
        var root = doc.RootElement;

        Assert.Equal(1002, root.GetProperty("id").GetInt64());
        Assert.Equal(2, root.GetProperty("clearnumber").GetInt64());
        Assert.Equal(8247907, root.GetProperty("initseed").GetInt64());
        // Both dates are JSON null until they happen - the capture never sends "".
        Assert.Equal(JsonValueKind.Null, root.GetProperty("firstcleardate").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("startdate").ValueKind);
        // empty lists serialize as [] not null
        Assert.Equal(0, root.GetProperty("buffsetsbyegogift").GetArrayLength());
        Assert.Equal(0, root.GetProperty("extrarewardstate").GetArrayLength());
        // typed buff sets
        var set = root.GetProperty("buffsets")[0];
        Assert.Equal(3, set.GetProperty("setid").GetInt64());
        Assert.Equal(13, set.GetProperty("currentbuffids")[0].GetInt64());
        Assert.Equal(0, set.GetProperty("recentbuffid").GetInt64());
        // nested personality round-trips
        var p = root.GetProperty("personalities")[0];
        Assert.Equal(1, p.GetProperty("pid").GetInt64());
        Assert.Equal(9, p.GetProperty("es")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public void UpdateNodeDatas_RoundTrips_WithPassthroughEnemyAndBattleStates()
    {
        var node = new UpdateNodeDatas
        {
            nodeid = 3,
            status = new List<PrevStatusData> { new() { pid = 1, hp = 10000, lv = 60, sid = 4, pord = 1 } },
            enemy = JsonNode.Parse("""{"lastWave":2,"lastTurn":3,"abnoSaveDataList":[]}""")!,
            battleStates = new List<JsonNode> { JsonNode.Parse("""{"type":1,"state":"x"}""")! },
            nodestate = 1,
        };
        var json = JsonSerializer.Serialize(node, global::PacketJson.Options);
        var back = JsonSerializer.Deserialize<UpdateNodeDatas>(json, global::PacketJson.Options)!;

        Assert.Equal(3, back.nodeid);
        Assert.Equal(10000, back.status[0].hp);
        Assert.Equal(4, back.status[0].sid);
        Assert.Equal(2, (long)back.enemy["lastWave"]!);
        Assert.Equal("x", (string?)back.battleStates[0]["state"]);
    }

    [Fact]
    public void UpdateNodeDatas_DefaultEnemy_SerializesAsEmptyObject()
    {
        var json = JsonSerializer.Serialize(new UpdateNodeDatas { nodeid = 0 }, global::PacketJson.Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("enemy").ValueKind);
        Assert.Empty(doc.RootElement.GetProperty("enemy").EnumerateObject());
    }
}
