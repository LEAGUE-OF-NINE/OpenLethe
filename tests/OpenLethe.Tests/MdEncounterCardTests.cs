using System.Linq;
using OpenLethe.Server;
using OpenLethe.Server.MirrorDungeon.Data;
using Xunit;

namespace OpenLethe.Tests;

public class MdEncounterCardTests
{
    [Fact]
    public void EncounterRewardMap_LoadsKnownCostCard()
    {
        var reward = MdEncounterCard.EncounterRewardMap[101];
        Assert.Equal("COST", reward.rewardType);
        Assert.Equal(1, reward.rewardLV);
        Assert.Equal("dungeon_battle_reward_case_format_cost_acquisition", reward.localizeTextFormat);
        Assert.Equal(80, reward.rewardParams!.acquireCostMin);
        Assert.Equal(120, reward.rewardParams!.acquireCostMax);
    }

    [Fact]
    public void EncounterRewardMap_LoadsKnownEgoStockCard()
    {
        var reward = MdEncounterCard.EncounterRewardMap[507];
        Assert.Equal("EGOSTOCK", reward.rewardType);
        Assert.Equal("dungeon_battle_reward_case_format_least_ego_resource_acquisition", reward.localizeTextFormat);
        Assert.Equal(4, reward.rewardParams!.leastEgoStock!.kind);
        Assert.Equal(10, reward.rewardParams!.leastEgoStock!.num);
    }

    [Fact]
    public void PickRandomEncounterCards_ReturnsOnePerAllowedGroupWithinLevel()
    {
        var cards = MdEncounterCard.PickRandomEncounterCards(7);
        Assert.NotEmpty(cards);
        // At most one card per allowed localizeTextFormat group.
        Assert.True(cards.Count <= MdEncounterCard.AllowedCardTypes.Count);
        Assert.Equal(cards.Count, cards.Distinct().Count());

        var formats = cards.Select(id => MdEncounterCard.EncounterRewardMap[id].localizeTextFormat).ToList();
        Assert.Equal(formats.Count, formats.Distinct().Count());
        Assert.All(formats, f => Assert.Contains(f, MdEncounterCard.AllowedCardTypes));
    }

    [Fact]
    public void PickRandomEncounterCards_RespectsMaxLevel()
    {
        var cards = MdEncounterCard.PickRandomEncounterCards(1);
        Assert.All(cards, id => Assert.True(MdEncounterCard.EncounterRewardMap[id].rewardLV <= 1));
    }

    [Fact]
    public void PickRandomEncounterCards_LevelZero_ReturnsNothing()
    {
        Assert.Empty(MdEncounterCard.PickRandomEncounterCards(0));
    }

    [Fact]
    public void StarlightMinMaxCards_AreReachable()
    {
        // Task 11b: the md-extreme capture's GetBattleRewardCase pools contain group-D
        // STARLIGHT_MIN_MAX ids directly (405/406/407), disproving the earlier "unreachable"
        // claim. chip_acquisition is now in AllowedCardTypes, so these cards are selectable.
        var starlightRewards = MdEncounterCard.EncounterRewardMap.Values
            .Where(r => r.rewardType == "STARLIGHT_MIN_MAX")
            .ToList();
        Assert.NotEmpty(starlightRewards);
        Assert.All(starlightRewards, r => Assert.Contains(r.localizeTextFormat, MdEncounterCard.AllowedCardTypes));

        // With every group above lv1 in scope and includeStarlightMinMax opted in (as
        // ExitMapNode's GetBattleRewardCase pool does), group D is one of the picked groups,
        // so the chip_acquisition format appears in the candidate set.
        var picked = MdEncounterCard.PickRandomEncounterCards(7, includeStarlightMinMax: true);
        var formats = picked.Select(id => MdEncounterCard.EncounterRewardMap[id].localizeTextFormat).ToHashSet();
        Assert.Contains("dungeon_battle_reward_case_format_chip_acquisition", formats);
    }

    [Fact]
    public void PickRandomEncounterCards_DefaultNeverLeaksGroupD()
    {
        // Regression guard for the shared-AllowedCardTypes leak: the OTHER caller
        // (AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon) uses the default param and must
        // keep its original 4-group set - its downstream switch has no STARLIGHT_MIN_MAX
        // case, so a leaked group-D pick (ids 401-407) would silently no-op.
        for (var i = 0; i < 50; i++)
        {
            var cards = MdEncounterCard.PickRandomEncounterCards(7);
            Assert.All(cards, id => Assert.NotEqual(4, id / 100));
        }

        // ExitMapNode's opted-in path can roll a group-D id.
        var sawGroupD = Enumerable.Range(0, 200)
            .SelectMany(_ => MdEncounterCard.PickRandomEncounterCards(7, includeStarlightMinMax: true))
            .Any(id => id / 100 == 4);
        Assert.True(sawGroupD);
    }

    [Theory]
    [InlineData(1, 3, 0, false)]
    [InlineData(1, 3, 1, true)]
    [InlineData(1, 3, 3, true)]
    [InlineData(1, 3, 4, false)]
    public void TierRange_WithinRange(long min, long max, long value, bool expected)
    {
        Assert.Equal(expected, new MdTierRange { min = min, max = max }.WithinRange(value));
    }
}
