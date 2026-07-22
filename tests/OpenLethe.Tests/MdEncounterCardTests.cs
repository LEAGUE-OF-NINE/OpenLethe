using System.Linq;
using OpenLethe.Server;
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
