// /api/GetStartBuffFInfoMirrorDungeon  ReqPacket_GetStartBuffFInfoMirrorDungeon -> ResPacket_GetStartBuffFInfoMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetStartBuffFInfoMirrorDungeon
{
    public int dungeonid;
    public long PacketId;
}

public class ResPacket_GetStartBuffFInfoMirrorDungeon
{
    public MirrorDungeonStartBuffInfoFormat startBuffInfo;
    public long PacketId;
}

