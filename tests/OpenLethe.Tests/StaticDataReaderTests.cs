using OpenLethe.Resources;
using Xunit;

public class StaticDataReaderTests
{
    [Fact]
    public void GetList_ReadsEgoFolder_NonEmpty_AllPositiveIds()
    {
        var egos = StaticData.GetList<IdStruct>("static-data/ego/");

        Assert.NotEmpty(egos);
        Assert.All(egos, e => Assert.True(e.id > 0));
    }

    [Fact]
    public void GetList_ReadsStageNodeReward_NonEmpty()
    {
        var nodes = StaticData.GetList<NodeIdStruct>("static-data/stagenodereward/");

        Assert.NotEmpty(nodes);
        Assert.All(nodes, n => Assert.True(n.nodeid > 0));
    }

    [Fact]
    public void GetList_UnknownFolder_ReturnsEmpty()
    {
        Assert.Empty(StaticData.GetList<IdStruct>("static-data/does-not-exist/"));
    }
}
