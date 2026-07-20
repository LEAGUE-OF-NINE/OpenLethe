// /api/AcquireStartEgoGiftsStoryMirrorDungeon  ReqPacket_AcquireStartEgoGiftsStoryMirrorDungeon -> ResPacket_AcquireStartEgoGiftsStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcquireStartEgoGiftsStoryMirrorDungeon
{
    public int selectedSetId;
    public List<int> selectedEgoGiftIds;
    public long PacketId;
}

public class ResPacket_AcquireStartEgoGiftsStoryMirrorDungeon
{
    public List<DungeonMapEgoGiftFormat> egoGifts;
    public List<MirrorDungeonEgoGiftPoolSetFormat> startEgoGiftPoolSets;
    public int startEgoGiftCreatedCount;
    public long PacketId;
    public List<DungeonMapEgoGiftFormat> EgoGifts;
    public List<MirrorDungeonEgoGiftPoolSetFormat> StartEgoGiftPoolSets;
    public int StartEgoGiftCreatedCount;
}

