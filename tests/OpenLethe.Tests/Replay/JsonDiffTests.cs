using System.Text.Json.Nodes;
using OpenLethe.Tests.Replay;

public class JsonDiffTests
{
    static JsonNode N(string s) => JsonNode.Parse(s)!;

    [Fact]
    public void Identical_NoDiffs() =>
        Assert.Empty(JsonDiff.Compare(N("{\"a\":1,\"b\":[1,2]}"), N("{\"a\":1,\"b\":[1,2]}"), []));

    [Fact]
    public void ValueMismatch_Reported()
    {
        var d = JsonDiff.Compare(N("{\"a\":1}"), N("{\"a\":2}"), []);
        Assert.Contains("a", d);
    }

    [Fact]
    public void MaskedLeaf_Ignored() =>
        Assert.Empty(JsonDiff.Compare(N("{\"a\":1}"), N("{\"a\":2}"), ["a"]));

    [Fact]
    public void WildcardArrayMask_Ignored() =>
        Assert.Empty(JsonDiff.Compare(
            N("{\"s\":[{\"id\":1},{\"id\":9}]}"),
            N("{\"s\":[{\"id\":2},{\"id\":8}]}"),
            ["s[*].id"]));

    [Fact]
    public void MissingKey_Reported()
    {
        var d = JsonDiff.Compare(N("{\"a\":1}"), N("{\"a\":1,\"b\":2}"), []);
        Assert.Contains("b", d);
    }
}
