using System.Collections.Generic;
using System.Text.Json;
using OpenLethe.Server.Wire;
using Xunit;

public class MirrorDungeonWireTests
{
    [Fact]
    public void MirrorOriginSaveInfo_SerializesRustFieldNames()
    {
        var save = new MirrorOriginSaveInfo
        {
            dungeonId = 4,
            idx = 1,
            version = 2,
            encounterstatistics = new List<long> { 0, 0, 0 },
            currentInfo = new CurrentInfo
            {
                cost = 500,
                startKeyword = "None",
                seps = new List<StartEgoGiftPoolSets> { new() { setId = 0, keyword = "Combustion", pool = new() { 9001 } } },
                ess = new List<EgoSkillStock> { new() { t = "CR", n = 0 } },
                cfs = new List<Cfs> { new() { floor = -1, difficulty = 0 } },
                dul = new List<Dungeonunitlist1> { new() { pid = 7 } },
                shop = new ShopInfo(),
            },
            dungeonMap = new DungeonMap { ns = new List<Ns> { new() { nid = 5, e = 10 } } },
        };
        var json = JsonSerializer.Serialize(save, global::PacketJson.Options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(4, root.GetProperty("dungeonId").GetInt64());
        Assert.Equal(2, root.GetProperty("version").GetInt64());
        var ci = root.GetProperty("currentInfo");
        Assert.Equal(500, ci.GetProperty("cost").GetInt64());
        Assert.Equal("None", ci.GetProperty("startKeyword").GetString());
        Assert.Equal("Combustion", ci.GetProperty("seps")[0].GetProperty("keyword").GetString());
        Assert.Equal(-1, ci.GetProperty("cfs")[0].GetProperty("floor").GetInt64());
        Assert.Equal(7, ci.GetProperty("dul")[0].GetProperty("pid").GetInt64());
        Assert.Equal(JsonValueKind.Array, ci.GetProperty("egs").ValueKind); // [] not null
        Assert.Equal(5, root.GetProperty("dungeonMap").GetProperty("ns")[0].GetProperty("nid").GetInt64());

        // round-trip
        var back = JsonSerializer.Deserialize<MirrorOriginSaveInfo>(json, global::PacketJson.Options)!;
        Assert.Equal(500, back.currentInfo.cost);
        Assert.Single(back.currentInfo.seps);
    }
}
