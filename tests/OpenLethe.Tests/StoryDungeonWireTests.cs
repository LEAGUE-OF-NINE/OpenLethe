using System.Collections.Generic;
using System.Text.Json;
using OpenLethe.Server.Wire;
using Xunit;

public class StoryDungeonWireTests
{
    [Fact]
    public void StorySaveInfo_SerializesRustFieldNames()
    {
        var save = new StorySaveInfo
        {
            dungeonid = 10501,
            currentinfo = new Currentinfo
            {
                cn = new Currentnode { f = 1, s = 2, nid = 10505 },
                pnids = new List<long> { 10501, 10505 },
                nr = 1,
            },
        };
        var json = JsonSerializer.Serialize(save, global::PacketJson.Options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(10501, root.GetProperty("dungeonid").GetInt64());
        var ci = root.GetProperty("currentinfo");
        Assert.Equal(10505, ci.GetProperty("cn").GetProperty("nid").GetInt64());
        Assert.Equal(2, ci.GetProperty("pnids").GetArrayLength());
        Assert.Equal(JsonValueKind.Array, ci.GetProperty("egs").ValueKind); // empty [] not null
    }
}
