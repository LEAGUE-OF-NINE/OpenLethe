using System.Linq;
using OpenLethe.Server;
using Xunit;

namespace OpenLethe.Tests;

public class StoryMdThemeDataTests
{
    // Verified against static-data/dungeon/*.json: exactly these entries carry
    // dungeonType == STORY_DUNGEON && dungeonLogicType == MIRROR_DUNGEON.
    private static readonly long[] Expected =
    {
        910301, 910302, 910701, 910901, 911401, 911501, 911801, 912014, 912114, 912401, 912711,
    };

    [Fact]
    public void StoryMdDungeonIds_DiscoversExactlyTheElevenMirrorLogicStoryDungeons()
    {
        Assert.Equal(Expected.OrderBy(x => x), StoryMdThemeData.StoryMdDungeonIds.OrderBy(x => x));
    }

    [Theory]
    [InlineData(910301)] [InlineData(910302)] [InlineData(910701)] [InlineData(910901)]
    [InlineData(911401)] [InlineData(911501)] [InlineData(911801)] [InlineData(912014)]
    [InlineData(912114)] [InlineData(912401)] [InlineData(912711)]
    public void GetTheme_EveryDungeon_HasNonEmptyBattleEventAndBossPools(long dungeonId)
    {
        var theme = StoryMdThemeData.GetTheme(dungeonId);
        Assert.Equal(dungeonId, theme.id);
        // battlePool being non-empty is what guarantees GenerateBattleNode terminates.
        Assert.NotEmpty(theme.battlePool);
        Assert.NotEmpty(theme.eventPool);
        Assert.NotEmpty(theme.bossPool);
    }

    [Theory]
    [InlineData(910301)] [InlineData(910302)] [InlineData(910701)] [InlineData(910901)]
    [InlineData(911401)] [InlineData(911501)] [InlineData(911801)] [InlineData(912014)]
    [InlineData(912114)] [InlineData(912401)] [InlineData(912711)]
    public void GetTheme_EveryDungeon_HasNonEmptyEgoGiftPool(long dungeonId)
    {
        // Non-empty everywhere is what replaces the upstream gen_range(0..0) panic.
        Assert.NotEmpty(StoryMdThemeData.GetTheme(dungeonId).GetCombinedEgoGiftPool());
    }

    [Theory]
    [InlineData(912014)]
    [InlineData(912114)]
    public void GetTheme_PilgrimageDungeons_FallBackToTheMirrorDungeonDropPool(long dungeonId)
    {
        // These two match zero entries in acquirable-egogifts-in-event, so their pool must
        // come from the MD drop-pool fallback - which is far larger than an event-derived one.
        Assert.True(StoryMdThemeData.GetTheme(dungeonId).egoGiftPool.Count > 50);
    }

    [Fact]
    public void GetTheme_EventDerivedDungeon_UsesTheEventJoinNotTheFallback()
    {
        // 911401 matches all 7 of its event nodes -> a small curated pool, not the big fallback.
        var theme = StoryMdThemeData.GetTheme(911401);
        Assert.NotEmpty(theme.egoGiftPool);
        Assert.True(theme.egoGiftPool.Count < 50);
    }

    [Fact]
    public void GetTheme_UnknownDungeonId_ReturnsEmptyPoolsWithoutThrowing()
    {
        var theme = StoryMdThemeData.GetTheme(123456);
        Assert.Empty(theme.battlePool);
        Assert.Empty(theme.bossPool);
    }
}
