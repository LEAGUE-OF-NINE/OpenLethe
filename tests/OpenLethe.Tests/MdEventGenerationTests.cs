using OpenLethe.Server;
using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.Wire;

// Event nodes of a generated floor: how many, which pool they draw from, how floor-specific
// events are weighted and which events a run sees only once. RNG is Random.Shared, so these
// assert counts, membership and generous frequency bounds, never exact ids.
public class MdEventGenerationTests
{
    private static List<Ns> Floor(long themeId, MirrorOriginSaveInfo? save = null)
    {
        save ??= new MirrorOriginSaveInfo();
        var before = save.dungeonMap.ns.Count;
        MdMapGen.GenerateNewFloor(3, themeId, save);
        return save.dungeonMap.ns.Skip(before).ToList();
    }

    private static int WidthOf(ThemeStatic t) => (int)t.mapGenSequence[0].numberList[0];

    [Theory]
    [InlineData(1001L, 3)] // The Forgotten, width 4
    [InlineData(1012L, 3)] // The Unchanging, width 5
    [InlineData(1005L, 2)] // The Unloving, width 3
    public void Md4Floor_PlacesAFixedNumberOfEvents(long themeId, int expected)
    {
        for (var i = 0; i < 200; i++)
            Assert.Equal(expected, Floor(themeId).Count(n => n.e == 3));
    }

    [Fact]
    public void EveryMd4PackWithEnoughEvents_GetsItsEventCount()
    {
        foreach (var theme in MdThemeData.AllThemes().Where(t => t.IsMd4Floor))
        {
            var distinct = theme.mapGenOption.eventPool.Concat(theme.mapGenOption.specialEventPool ?? new()).Distinct().Count();
            var expected = distinct == 0 ? 0 : MdMapGen.Md4EventCount(WidthOf(theme));
            if (distinct > 0 && distinct < expected) continue;
            for (var i = 0; i < 20; i++)
                Assert.True(expected == Floor(theme.id).Count(n => n.e == 3), $"theme {theme.id}");
        }
    }

    [Fact]
    public void FloorSpecificEvents_AreTheOnesFewPacksList()
    {
        foreach (var id in new long[] { 901015, 901062, 901100, 901104, 971056, 971088 })
            Assert.True(MdEventPool.IsFloorSpecific(id), $"{id}");
        foreach (var id in new long[] { 901001, 901030, 971055 })
            Assert.False(MdEventPool.IsFloorSpecific(id), $"{id}");
    }

    [Fact]
    public void FloorSpecificEvents_AreBoostedOnTheirFloor()
    {
        // 1021 The Dream Ending: 6 of its 26 events are floor-specific (901100-901104, 971056).
        // Unweighted they would fill ~23% of its event nodes; at x20 well over half.
        var theme = MdThemeData.GetThemeById(1021)!;
        var specific = theme.mapGenOption.eventPool.Count(MdEventPool.IsFloorSpecific);
        Assert.Equal(6, specific);

        var eids = Enumerable.Range(0, 200).SelectMany(_ => Floor(1021).Where(n => n.e == 3).Select(n => n.eid)).ToList();
        var share = eids.Count(MdEventPool.IsFloorSpecific) / (double)eids.Count;
        Assert.True(share > 0.6, $"floor-specific share {share:P0}");
    }

    [Fact]
    public void Md4SpecialEventPool_IsDrawnWithItsProbability()
    {
        // 1101 Hell's Chicken: specialEventPool [971001, 971003], specialEventProb 0.75. The MD4
        // generator used to ignore the special pool entirely.
        var theme = MdThemeData.GetThemeById(1101)!;
        var special = theme.mapGenOption.specialEventPool!;
        var floors = Enumerable.Range(0, 200).Select(_ => Floor(1101)).ToList();
        var withSpecial = floors.Count(f => f.Any(n => n.e == 3 && special.Contains(n.eid)));
        Assert.True(withSpecial > 160, $"{withSpecial}/200 floors had a special event");
    }

    [Fact]
    public void PacksWithSpecialEventProb_GetNoFloorSpecificBoost()
    {
        // 1029 An Anarchy of Colors lists floor-specific events (901100-901104) in its eventPool,
        // but its specialEventPool/specialEventProb already decide its own events.
        var theme = MdThemeData.GetThemeById(1029)!;
        Assert.NotNull(theme.mapGenOption.specialEventProb);
        var picker = new MdEventPicker(theme);
        Assert.False(picker.UsesFloorSpecificWeighting);
        Assert.Equal(1, picker.WeightOf(901100));

        Assert.True(new MdEventPicker(MdThemeData.GetThemeById(1021)!).UsesFloorSpecificWeighting);
        Assert.Equal(MdEventPicker.FloorSpecificWeight, new MdEventPicker(MdThemeData.GetThemeById(1021)!).WeightOf(901100));
    }

    [Theory]
    [InlineData(901058L)]
    [InlineData(901068L)]
    [InlineData(901074L)] // Clockwork Spring's +2 (993003)
    [InlineData(901091L)]
    [InlineData(971055L)] // Wealth (9800)
    public void LevelUpAndWealthEvents_AreOncePerRun(long eventId)
    {
        Assert.True(MdEventPool.IsOncePerRun(eventId));
    }

    [Fact]
    public void OrdinaryEvents_AreNotOncePerRun()
    {
        Assert.False(MdEventPool.IsOncePerRun(901021));
        Assert.False(MdEventPool.IsOncePerRun(901001)); // its 991002 is a floor-scoped gift
    }

    [Fact]
    public void PlayedOncePerRunEvents_NeverSpawnAgain()
    {
        var once = new long[] { 901058, 901068, 901074, 901091, 971055 };
        var themes = MdThemeData.AllThemes()
            .Where(t => t.IsMd4Floor && t.mapGenOption.eventPool.Intersect(once).Any())
            .Select(t => t.id).ToList();
        Assert.NotEmpty(themes);

        foreach (var themeId in themes)
            for (var i = 0; i < 30; i++)
            {
                var save = new MirrorOriginSaveInfo();
                save.currentInfo.peids.AddRange(once);
                Assert.DoesNotContain(Floor(themeId, save), n => n.e == 3 && once.Contains(n.eid));
            }
    }

    [Fact]
    public void OwningTheGift_SpendsTheEventToo()
    {
        // Wealth (9800) owned - its event 971055 is out even though the run never played it.
        for (var i = 0; i < 200; i++)
        {
            var save = new MirrorOriginSaveInfo();
            save.currentInfo.egs.Add(new AcquiredEgogifts { id = 9800 });
            Assert.DoesNotContain(Floor(1001, save), n => n.eid == 971055);
        }
    }

    [Fact]
    public void Spent_OnlyListsOncePerRunEvents()
    {
        var spent = MdEventPool.Spent(new long[] { 901074, 901021, 971055 }, new long[] { 901074, 901021 }, new long[] { 9800 });
        Assert.Equal(new HashSet<long> { 901074, 971055 }, spent);
    }

    [Fact]
    public void FixedRewardEgoGifts_ReplaceTheRandomFloorRewards()
    {
        // The B.E. (3001) reaches the server as an uploaded pack; its floor clear offers exactly
        // Bongy Plush, Lunar Memory, Faith and Piece of Relationship.
        var fixedGifts = new List<long> { 9242, 9083, 9081, 9082 };
        var json = "[{\"id\":3001,\"fixedRewardEgoGifts\":[9242,9083,9081,9082],\"egoGiftPool\":[9001,9048,9051,9066]}]";
        var uploaded = AccountFields.Get<List<ThemeStatic>>(json)!;
        Assert.Equal(fixedGifts, uploaded[0].fixedRewardEgoGifts);

        var pool = new MdThemePool(uploaded);
        Assert.Equal(fixedGifts, pool.FloorRewardEgos(3001, 3, 0));
        Assert.Equal(fixedGifts, pool.FloorRewardEgos(3001, 4, 7));
        // A regular pack still rolls its count.
        Assert.Equal(3, pool.FloorRewardEgos(1001, 3, 0).Count);
    }
}
