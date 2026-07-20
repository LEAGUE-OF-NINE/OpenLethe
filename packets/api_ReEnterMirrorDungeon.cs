// /api/ReEnterMirrorDungeon  ReqPacket_ReEnterMirrorDungeon -> ResPacket_ReEnterMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ReEnterMirrorDungeon
{
    public int dungeonid;
    public int idx;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_ReEnterMirrorDungeon
{
    public MirrorDungeonSaveInfoFormat saveInfo;
    public long PacketId;
    public MirrorDungeonSaveInfoFormat SaveInfo;
}

