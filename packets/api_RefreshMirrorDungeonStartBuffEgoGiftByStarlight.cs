// /api/RefreshMirrorDungeonStartBuffEgoGiftByStarlight  ReqPacket_RefreshMirrorDungeonStartBuffEgoGiftByStarlight -> ResPacket_RefreshMirrorDungeonStartBuffEgoGiftByStarlight
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RefreshMirrorDungeonStartBuffEgoGiftByStarlight
{
    public long PacketId;
}

public class ResPacket_RefreshMirrorDungeonStartBuffEgoGiftByStarlight
{
    public MirrorDungeonSaveInfoFormat saveInfo;
    public long PacketId;
    public MirrorDungeonSaveInfoFormat SaveInfo;
}

