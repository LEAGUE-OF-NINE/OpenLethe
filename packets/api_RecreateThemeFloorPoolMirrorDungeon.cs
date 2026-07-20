// /api/RecreateThemeFloorPoolMirrorDungeon  ReqPacket_RecreateThemeFloorPoolMirrorDungeon -> ResPacket_RecreateThemeFloorPoolMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RecreateThemeFloorPoolMirrorDungeon
{
    public long PacketId;
}

public class ResPacket_RecreateThemeFloorPoolMirrorDungeon
{
    public MirrorDungeonSaveInfoFormat saveInfo;
    public long PacketId;
    public MirrorDungeonSaveInfoFormat SaveInfo;
}

