using System.Collections.Generic;
using System.Text.Json;
using OpenLethe.Server.Wire;
using Xunit;

public class RailwayWireTests
{
    [Fact]
    public void RailwaySaveInfo_SerializesRustFieldNames_ListsAsEmptyArrays()
    {
        var save = new RailwaySaveInfo
        {
            id = 5,
            personalities = new List<Personalities> { new() { pid = 1, es = new List<Egos> { new() { id = 9, g = 1, idx = 0 } } } },
            initseed = 57515885,
            currentseed = 57515885,
        };

        var json = JsonSerializer.Serialize(save, global::PacketJson.Options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(5, root.GetProperty("id").GetInt64());
        Assert.Equal(57515885, root.GetProperty("initseed").GetInt64());
        Assert.Equal(57515885, root.GetProperty("currentseed").GetInt64());
        // empty lists serialize as [] not null
        Assert.Equal(JsonValueKind.Array, root.GetProperty("buffsetsbyegogift").ValueKind);
        Assert.Equal(0, root.GetProperty("buffsetsbyegogift").GetArrayLength());
        Assert.Equal("", root.GetProperty("firstcleardate").GetString());
        // nested personality round-trips
        var p = root.GetProperty("personalities")[0];
        Assert.Equal(1, p.GetProperty("pid").GetInt64());
        Assert.Equal(9, p.GetProperty("es")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public void UpdateNodeDatas_RoundTrips_WithPassthroughEnemy()
    {
        var node = new UpdateNodeDatas
        {
            nodeid = 3,
            status = new List<PrevStatusData> { new() { pid = 1, hp = 10000, lv = 60, pord = 1 } },
            nodestate = 1,
        };
        var json = JsonSerializer.Serialize(node, global::PacketJson.Options);
        var back = JsonSerializer.Deserialize<UpdateNodeDatas>(json, global::PacketJson.Options)!;
        Assert.Equal(3, back.nodeid);
        Assert.Equal(10000, back.status[0].hp);
        Assert.Equal(1, back.nodestate);
        Assert.NotNull(back.enemy);
        Assert.Empty(back.enemy.abnoSaveDataList);
    }
}
