using System.Text.Json;
using OpenLethe.Server;
using OpenLethe.Server.Wire;
using Xunit;

public class MdShopDataTests
{
    [Fact]
    public void GetById_ReturnsRealEgo()
    {
        // static-data/ego-gift-mirrordungeon/ego-gift-mirrordungeon.json id 9001, price 198
        var ego = MdEgoData.GetById(9001);
        Assert.NotNull(ego);
        Assert.Equal(198, ego!.price);
    }

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        Assert.Null(MdEgoData.GetById(999999999));
    }

    [Fact]
    public void UpgradeCost_MatchesFormula()
    {
        // ((price * desiredUl / 3) / 10) * 10, integer division at each step
        Assert.Equal(60, MdEgoData.UpgradeCost(198, 1)); // (198*1/3)/10*10 = 66/10*10 = 6*10 = 60
        Assert.Equal(0, MdEgoData.UpgradeCost(198, 0));
    }

    [Fact]
    public void TierUpgradeCost_MatchesStaticEgoGiftUpgradeCostTable()
    {
        // Byte-verified against tests/OpenLethe.Tests/fixtures/md-extreme-run.jsonl seq
        // 98/99/156-159/209-212 (11 of 12 UpgradeEgoGiftMirrorDungeon records): cost is
        // NOT price-based (the old UpgradeCost formula only matched 3/12) - it's a flat
        // per-tier lookup from mirror-dungeon-common-data-md7.json egoGiftUpgradeCostTable,
        // keyed by MdEgoData.DetermineEgoTier, cost[desiredUl-1].
        Assert.Equal(50, MdEgoData.TierUpgradeCost(9101, 1)); // TIER_1 -> [50,100]
        Assert.Equal(60, MdEgoData.TierUpgradeCost(9009, 1)); // TIER_2 -> [60,120]
        Assert.Equal(120, MdEgoData.TierUpgradeCost(9009, 2));
        Assert.Equal(60, MdEgoData.TierUpgradeCost(9001, 1)); // TIER_2
        Assert.Equal(120, MdEgoData.TierUpgradeCost(9001, 2));
        Assert.Equal(75, MdEgoData.TierUpgradeCost(9053, 1)); // TIER_3 -> [75,150]
        Assert.Equal(150, MdEgoData.TierUpgradeCost(9053, 2));
        Assert.Equal(75, MdEgoData.TierUpgradeCost(9111, 1)); // TIER_3
        Assert.Equal(150, MdEgoData.TierUpgradeCost(9111, 2));
        Assert.Equal(100, MdEgoData.TierUpgradeCost(9752, 1)); // TIER_4 -> [100,200]
        Assert.Equal(200, MdEgoData.TierUpgradeCost(9752, 2));
    }

    [Theory]
    [InlineData(1, 0, 60)]
    [InlineData(2, 0, 120)]
    [InlineData(5, 0, 100)]
    [InlineData(6, 0, 200)]
    [InlineData(6, 1, 240)]
    [InlineData(6, 2, 300)]
    [InlineData(6, 3, 400)]
    [InlineData(6, 9, 0)] // floor OOB
    [InlineData(14, 0, 150)]
    [InlineData(999, 0, 0)] // unmatched node
    public void GetDefaultCost_MatchesRustArms(long nodeE, long floor, long expected)
    {
        Assert.Equal(expected, MdCost.GetDefaultCost(nodeE, floor));
    }

    [Fact]
    public void AcquireRewardEgoGiftsResult_SerializesLowercaseSaveinfo()
    {
        var result = new AcquireRewardEgoGiftsMirrorDungeonResult();
        var json = JsonSerializer.Serialize(result, global::PacketJson.Options);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("saveinfo", out _));
        Assert.True(doc.RootElement.TryGetProperty("egoGifts", out _));
        Assert.True(doc.RootElement.TryGetProperty("dungeonUnitList", out _));
    }

    [Fact]
    public void PurchaseEgoGiftResult_SerializesLowercaseEgogifts()
    {
        var result = new PurchaseEgoGiftMirrorDungeonResult();
        var json = JsonSerializer.Serialize(result, global::PacketJson.Options);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("egogifts", out _));
        Assert.True(doc.RootElement.TryGetProperty("usedcost", out _));
        Assert.True(doc.RootElement.TryGetProperty("shopInfo", out _));
    }

    [Fact]
    public void UpgradeEgoGiftResult_SerializesEgoGiftAndUsedcost()
    {
        var result = new UpgradeEgoGiftMirrorDungeonResult();
        var json = JsonSerializer.Serialize(result, global::PacketJson.Options);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("egoGift", out _));
        Assert.True(doc.RootElement.TryGetProperty("usedcost", out _));
        Assert.True(doc.RootElement.TryGetProperty("dungeonUnitList", out _));
    }
}
