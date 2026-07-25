// /api/RefreshStartEgoGiftsStoryMirrorDungeon  ReqPacket_RefreshStartEgoGiftsStoryMirrorDungeon -> ResPacket_RefreshStartEgoGiftsStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RefreshStartEgoGiftsStoryMirrorDungeon
{
    public long PacketId;
}

public class ResPacket_RefreshStartEgoGiftsStoryMirrorDungeon
{
    public List<MirrorDungeonEgoGiftPoolSetFormat> startEgoGiftPoolSets;
    public int startEgoGiftCreatedCount;
    public long PacketId;
    public List<MirrorDungeonEgoGiftPoolSetFormat> StartEgoGiftPoolSets;
    public int StartEgoGiftCreatedCount;
}

