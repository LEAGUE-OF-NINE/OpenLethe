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
