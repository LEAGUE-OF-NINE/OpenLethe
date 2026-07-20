// /api/RemoveMirrorDungeonEgoGift  ReqPacket_RemoveMirrorDungeonEgoGift -> ResPacket_RemoveMirrorDungeonEgoGift
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RemoveMirrorDungeonEgoGift
{
    public int egogiftId;
    public long PacketId;
}

public class ResPacket_RemoveMirrorDungeonEgoGift
{
    public List<DungeonMapEgoGiftFormat> egs;
    public long PacketId;
    public List<DungeonMapEgoGiftFormat> EgoGifts;
}

