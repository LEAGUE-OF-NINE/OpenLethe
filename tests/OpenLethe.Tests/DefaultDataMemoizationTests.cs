using OpenLethe.Server.Defaults;
using Xunit;

namespace OpenLethe.Tests;

public class DefaultDataMemoizationTests
{
    [Fact]
    public void Builders_ReturnEqualContent_ButFreshListInstances()
    {
        var a = DefaultData.GetFormattedEgos();
        var b = DefaultData.GetFormattedEgos();

        Assert.NotSame(a, b);                       // fresh copy each call
        Assert.Equal(a.Count, b.Count);             // same content
        Assert.True(a.Count > 0);
        Assert.Equal(a[0].ego_id, b[0].ego_id);

        // Mutating the returned list must not affect the next call (cache intact).
        a.Clear();
        Assert.True(DefaultData.GetFormattedEgos().Count > 0);
    }

    [Fact]
    public void ChapterStateAndPersonalities_StillBuild()
    {
        Assert.NotEmpty(DefaultData.LoadMainChapterState());
        Assert.NotEmpty(DefaultData.GetFormattedPersonalities());
        Assert.NotEmpty(DefaultData.GetFormattedUserCodes());
        Assert.NotEmpty(DefaultData.GetDanteAbilityIds());
    }
}
