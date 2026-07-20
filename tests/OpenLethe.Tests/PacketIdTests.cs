using System.Collections.Generic;
using Xunit;

public class PacketIdTests
{
    [Fact]
    public void Extraction_FoundAllResponseImpls()
    {
        // 153 `impl HasPacketId for ResponseResultApi*` blocks in packets.rs.
        Assert.Equal(153, PacketIds.Count);
    }

    [Theory]
    // Spot-checked by hand against models/src/packets.rs.
    [InlineData("EnterBossRaid", 1696)]
    [InlineData("GetHellsChickenState", 8297822)]
    [InlineData("AcquireHellsChickenReward", 1117249)]
    [InlineData("AcquireRailwayDungeonReward", 4200)]
    public void KnownPacketIds_MatchRustSource(string name, long expected)
    {
        Assert.Equal(expected, PacketIds.For(name));
    }

    [Fact]
    public void MissingPacketId_ThrowsWithActionableMessage()
    {
        var ex = Assert.Throws<KeyNotFoundException>(() => PacketIds.For("NoSuchPacket"));
        Assert.Contains("extract-packet-ids", ex.Message);
    }
}
