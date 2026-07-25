// /api/EnterInfiniteMode  ReqPacket_EnterInfiniteMode -> ResPacket_EnterInfiniteMode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterInfiniteMode
{
    public int dungeonId;
    public long PacketId;
}

public class ResPacket_EnterInfiniteMode
{
    public MirrorDungeonSaveInfoFormat saveInfo;
    public long PacketId;
    public MirrorDungeonSaveInfoFormat SaveInfo;
}

