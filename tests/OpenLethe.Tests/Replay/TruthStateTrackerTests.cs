using System.Text.Json.Nodes;
using OpenLethe.Tests.Replay;
using Xunit;

public class TruthStateTrackerTests
{
    static JsonNode Res(string result) => JsonNode.Parse($"{{\"result\":{result}}}")!;

    [Fact]
    public void FullSaveInfo_Replaces()
    {
        var t = new TruthStateTracker();
        t.Advance("/api/EnterMirrorDungeon", Res("{\"saveInfo\":{\"dungeonId\":7,\"idx\":0}}"));
        Assert.Contains("\"dungeonId\":7", t.MdSaveInfoJson);
    }

    [Fact]
    public void CurrentInfoOnly_ReplacesSubtree()
    {
        var t = new TruthStateTracker();
        t.Advance("/api/EnterMirrorDungeon", Res("{\"saveInfo\":{\"dungeonId\":7,\"currentInfo\":{\"cost\":100}}}"));
        t.Advance("/api/ExitMirrorDungeonMapNode", Res("{\"currentInfo\":{\"cost\":250}}"));
        var save = JsonNode.Parse(t.MdSaveInfoJson)!;
        Assert.Equal(7, (int)save["dungeonId"]!);           // outer preserved
        Assert.Equal(250, (int)save["currentInfo"]!["cost"]!); // subtree replaced
    }

    [Fact]
    public void PartialDelta_MergesNamedFields()
    {
        var t = new TruthStateTracker();
        t.Advance("/api/EnterMirrorDungeon",
            Res("{\"saveInfo\":{\"currentInfo\":{\"cost\":100,\"egs\":[]}}}"));
        t.Advance("/api/PurchaseEgoGiftMirrorDungeon",
            Res("{\"cost\":80,\"egogifts\":[{\"id\":9009}]}"));
        var ci = JsonNode.Parse(t.MdSaveInfoJson)!["currentInfo"]!;
        Assert.Equal(80, (int)ci["cost"]!);
        Assert.Single(ci["egs"]!.AsArray());
    }
}
