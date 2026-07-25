// /api/DetectMirrorDungeonThemeFloorByStarlight  ReqPacket_DetectMirrorDungeonThemeFloorByStarlight -> ResPacket_DetectMirrorDungeonThemeFloorByStarlight
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_DetectMirrorDungeonThemeFloorByStarlight
{
    public int originThemeFloorId;
    public int detectThemeFloorId;
    public int difficulty;
    public long PacketId;
}

public class ResPacket_DetectMirrorDungeonThemeFloorByStarlight
{
    public MirrorDungeonCurrentInfoFormat currentInfo;
    public long PacketId;
    public MirrorDungeonCurrentInfoFormat CurrentInfo;
}

