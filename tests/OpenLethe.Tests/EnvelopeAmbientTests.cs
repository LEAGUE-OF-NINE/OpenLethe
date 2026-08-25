using System.Text.Json;

// ResponsePacket.Ok must emit the ambient `updated` / `synchronized` blocks. The real
// server sends them on 728/760 and 730/760 captured records; without them the client
// 200s a response but never advances (it re-sends SelectFormationMirrorDungeon forever
// instead of moving on to ReEnterMirrorDungeon).
//
// MdExtremeReplayTests CANNOT catch this: ReplayMasks.Always strips "updated" and
// "synchronized" from every comparison. This test is the only guard.
public class EnvelopeAmbientTests
{
    private static JsonElement Serialize() => JsonSerializer.SerializeToElement(
        global::ResponsePacket<object>.Ok(new object(), 67), global::PacketJson.Options);

    [Fact]
    public void Ok_EmitsAmbientUpdatedAndSynchronized()
    {
        var root = Serialize();

        Assert.True(root.TryGetProperty("updated", out var updated), "`updated` missing from envelope");
        Assert.True(root.TryGetProperty("synchronized", out var sync), "`synchronized` missing from envelope");

        // Shape of the dominant ambient block in both md-extreme captures (546/728 records).
        Assert.False(updated.GetProperty("isInitialized").GetBoolean());
        Assert.False(updated.GetProperty("isResetMirrorDungeon").GetBoolean());
        foreach (var empty in new[] { "mailList", "missionList", "userUnlockCodeList" })
            Assert.Equal(0, updated.GetProperty(empty).GetArrayLength());

        Assert.Equal(513, sync.GetProperty("version").GetInt32());
        Assert.Equal(0, sync.GetProperty("noticeList").GetArrayLength());
        Assert.Equal(0, sync.GetProperty("mailContentList").GetArrayLength());
    }

    [Fact]
    public void HandlerAssignedUpdated_StillOverridesTheAmbientDefault()
    {
        var packet = global::ResponsePacket<object>.Ok(new object(), 67);
        packet.updated = new OpenLethe.Server.Wire.UpdatedFormat { isInitialized = true };

        var updated = JsonSerializer.SerializeToElement(packet, global::PacketJson.Options)
            .GetProperty("updated");

        Assert.True(updated.GetProperty("isInitialized").GetBoolean());
        Assert.False(updated.TryGetProperty("mailList", out _)); // ambient default fully replaced
    }
}
