using OpenLethe.Server;
using Xunit;

public class StoryMapDataTests
{
    [Fact]
    public void GetStoryMapById_FindsEmbeddedMap()
    {
        var map = StoryMapData.GetStoryMapById(10501);
        Assert.NotNull(map);
        var startNode = map!.floors[0].sectors[0].nodes[0];
        Assert.Equal("10501", startNode.id);
        Assert.Equal("start", startNode.encounter);
    }

    [Fact]
    public void GetStoryFloorEncounterId_ReturnsNodeEncounterId()
    {
        Assert.Equal(0, StoryMapData.GetStoryFloorEncounterId(10501, "10501"));
    }

    [Fact]
    public void GetStoryMapById_UnknownMap_Null()
    {
        Assert.Null(StoryMapData.GetStoryMapById(999999));
    }

    [Fact]
    public void GetAbRewardsByNodeId_UnknownNode_Null()
    {
        Assert.Null(StoryMapData.GetAbRewardsByNodeId(10501, "not-a-node"));
    }
}
