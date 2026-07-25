// /login/CheckClientVersion  ReqPacket_CheckClientVersion -> ResPacket_CheckClientVersion
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_CheckClientVersion
{
    public long PacketId;
}

public class ResPacket_CheckClientVersion
{
    public long timeoffset;
    public long PacketId;
}

