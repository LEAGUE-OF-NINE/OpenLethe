using System.Text.Json;
using Xunit;

public class EnvelopeSerializationTests
{
    [Fact]
    public void PacketFields_AreSerialized_NotSilentlyDropped()
    {
        // Regression guard: without IncludeFields this serializes to "{}" exactly,
        // because the client-extracted types use public fields, not properties.
        var res = new ResPacket_EnterBossRaid
        {
            saveInfo = new BossRaidSaveDataFormat(),
            PacketId = 1696,
        };

        var json = JsonSerializer.Serialize(res, PacketJson.Options);

        Assert.NotEqual("{}", json);
        Assert.Contains("\"saveInfo\"", json);
    }

    [Fact]
    public void FieldNames_AreEmittedVerbatim()
    {
        var req = new ReqPacket_EnterBossRaid { raidId = 5 };
        var json = JsonSerializer.Serialize(req, PacketJson.Options);

        Assert.Contains("\"raidId\"", json);
        Assert.DoesNotContain("\"RaidId\"", json);
    }

    [Fact]
    public void MissingMembers_DefaultInsteadOfThrowing()
    {
        // Rust marks nearly every field #[serde(default)].
        var req = JsonSerializer.Deserialize<ReqPacket_EnterBossRaid>(
            "{\"raidId\":7}", PacketJson.Options);

        Assert.Equal(7, req!.raidId);
        Assert.Equal(0, req.difficulty);
    }

    [Fact]
    public void UnknownMembers_AreIgnored()
    {
        // The client may send fields we do not model; this must not 500.
        var req = JsonSerializer.Deserialize<ReqPacket_EnterBossRaid>(
            "{\"raidId\":7,\"totallyUnknownField\":123}", PacketJson.Options);

        Assert.Equal(7, req!.raidId);
    }

    [Fact]
    public void Envelope_OmitsNullOptionalMembers()
    {
        var env = ResponsePacket<ResPacket_EnterBossRaid>.Ok(
            new ResPacket_EnterBossRaid(), 1696);

        var json = JsonSerializer.Serialize(env, PacketJson.Options);

        Assert.DoesNotContain("updated", json);
        Assert.DoesNotContain("synchronized", json);
    }

    [Fact]
    public void Envelope_CarriesRequiredConstants()
    {
        var env = ResponsePacket<ResPacket_EnterBossRaid>.Ok(
            new ResPacket_EnterBossRaid(), 1696);

        var json = JsonSerializer.Serialize(env, PacketJson.Options);

        Assert.Contains("\"state\":\"ok\"", json);
        Assert.Contains("\"version\":\"product\"", json);
        Assert.Contains("\"packetId\":1696", json);
    }

    [Fact]
    public void Il2CppArrays_SerializeAsJsonArrays()
    {
        // Must match Rust's Vec<T> representation.
        var arr = new Il2CppStructArray<int> { 1, 2, 3 };
        Assert.Equal("[1,2,3]", JsonSerializer.Serialize(arr, PacketJson.Options));
    }
}
