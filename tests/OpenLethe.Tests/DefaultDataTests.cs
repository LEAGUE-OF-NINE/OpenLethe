using OpenLethe.Server.Defaults;
using Xunit;

public class DefaultDataTests
{
    [Fact]
    public void GetFormattedEgos_NonEmpty_WithDefaults()
    {
        var egos = DefaultData.GetFormattedEgos();

        Assert.NotEmpty(egos);
        Assert.All(egos, e =>
        {
            Assert.True(e.ego_id > 0);
            Assert.Equal(4, e.gacksung);
            Assert.Equal(DefaultData.AcquireTime, e.acquire_time);
        });
    }

    [Fact]
    public void GetFormattedPersonalities_OrderIdsAreSequential()
    {
        var ps = DefaultData.GetFormattedPersonalities();

        Assert.NotEmpty(ps);
        for (var i = 0; i < ps.Count; i++)
        {
            Assert.Equal(i, ps[i].order_id);
            Assert.Equal(60, ps[i].level);
            Assert.Equal(100, ps[i].exp);
        }
    }

    [Fact]
    public void GetDanteAbilityIds_NonEmpty()
    {
        Assert.NotEmpty(DefaultData.GetDanteAbilityIds());
    }

    [Fact]
    public void LoadMainChapterState_BuildsNestedTree()
    {
        var chapters = DefaultData.LoadMainChapterState();

        Assert.NotEmpty(chapters);
        var chapter = chapters[0];
        Assert.NotEmpty(chapter.subcss);
        var sub = chapter.subcss[0];
        Assert.Equal(new List<long> { 1, 2, 3, 10 }, sub.rss);
        Assert.NotEmpty(sub.nss);
        Assert.All(sub.nss, n => { Assert.Equal(2, n.ct); Assert.Equal(1, n.cn); });
    }
}
