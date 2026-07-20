// /api/SelectThemeFloorMirrorDungeon  ReqPacket_SelectThemeFloorMirrorDungeon -> ResPacket_SelectThemeFloorMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SelectThemeFloorMirrorDungeon
{
    public int selectedIdx;
    public int selectedThemeFoorId;
    public long PacketId;
}

public class ResPacket_SelectThemeFloorMirrorDungeon
{
    public MirrorDungeonSaveInfoFormat saveInfo;
    public long PacketId;
    public MirrorDungeonSaveInfoFormat SaveInfo;
}

