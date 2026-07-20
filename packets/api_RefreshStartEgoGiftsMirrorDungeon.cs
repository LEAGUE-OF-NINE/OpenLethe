// /api/RefreshStartEgoGiftsMirrorDungeon  ReqPacket_RefreshStartEgoGiftsMirrorDungeon -> ResPacket_RefreshStartEgoGiftsMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RefreshStartEgoGiftsMirrorDungeon
{
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_RefreshStartEgoGiftsMirrorDungeon
{
    public List<MirrorDungeonEgoGiftPoolSetFormat> startEgoGiftPoolSets;
    public int startEgoGiftCreatedCount;
    public long PacketId;
    public List<MirrorDungeonEgoGiftPoolSetFormat> StartEgoGiftPoolSets;
    public int StartEgoGiftCreatedCount;
}

