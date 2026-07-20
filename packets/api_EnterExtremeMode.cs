// /api/EnterExtremeMode  ReqPacket_EnterExtremeMode -> ResPacket_EnterExtremeMode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterExtremeMode
{
    public int dungeonId;
    public long PacketId;
}

public class ResPacket_EnterExtremeMode
{
    public MirrorDungeonSaveInfoFormat saveInfo;
    public long PacketId;
    public MirrorDungeonSaveInfoFormat SaveInfo;
}

