// /api/AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon  ReqPacket_AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon -> ResPacket_AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon
{
    public int selectedSetId;
    public List<int> selectedEgoGiftIds;
    public bool isEnableEgogiftDetectionToggle;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon
{
    public MirrorDungeonSaveInfoFormat saveInfo;
    public long PacketId;
    public MirrorDungeonSaveInfoFormat SaveInfo;
}

