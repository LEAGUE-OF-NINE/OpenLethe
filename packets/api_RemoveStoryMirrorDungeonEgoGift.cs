// /api/RemoveStoryMirrorDungeonEgoGift  ReqPacket_RemoveStoryMirrorDungeonEgoGift -> ResPacket_RemoveStoryMirrorDungeonEgoGift
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RemoveStoryMirrorDungeonEgoGift
{
    public int egogiftId;
    public long PacketId;
}

public class ResPacket_RemoveStoryMirrorDungeonEgoGift
{
    public List<DungeonMapEgoGiftFormat> egs;
    public long PacketId;
    public List<DungeonMapEgoGiftFormat> EgoGifts;
}

