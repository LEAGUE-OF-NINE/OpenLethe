// /api/SendMirrorDungeonLogError  ReqPacket_SendMirrorDungeonLogError -> ResPacket_SendMirrorDungeonLogError
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SendMirrorDungeonLogError
{
    public int type;
    public long PacketId;
}

public class ResPacket_SendMirrorDungeonLogError
{
    public long PacketId;
}

