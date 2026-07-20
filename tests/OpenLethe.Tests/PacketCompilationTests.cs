using System.Linq;
using Xunit;

public class PacketCompilationTests
{
    [Fact]
    public void PacketAssembly_ExposesExpectedTypeCount()
    {
        var asm = typeof(ReqPacket_EnterBossRaid).Assembly;
        var packetTypes = asm.GetTypes()
            .Where(t => t.Name.StartsWith("ReqPacket_") || t.Name.StartsWith("ResPacket_"))
            .ToList();

        // 214 packet files; every one declares a Req and/or Res type.
        Assert.True(packetTypes.Count > 200,
            $"Expected >200 packet types, found {packetTypes.Count}");
    }

    [Fact]
    public void Il2CppShim_BehavesAsList()
    {
        var refArr = new Il2CppReferenceArray<string> { "a", "b" };
        var structArr = new Il2CppStructArray<int> { 1, 2, 3 };

        Assert.Equal(2, refArr.Count);
        Assert.Equal(3, structArr.Count);
    }

    [Fact]
    public void SharedFormatTypes_AreResolvable()
    {
        // _shared.cs must compile; these are used across many endpoints.
        Assert.NotNull(new BossRaidSaveDataFormat());
        Assert.NotNull(new MirrorDungeonSaveInfoFormat());
        Assert.NotNull(new UserAuthFormat());
    }
}
