// /api/GetTheaterInfo  ReqPacket_GetTheaterInfo -> ResPacket_GetTheaterInfo
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetTheaterInfo
{
    public long PacketId;
}

public class ResPacket_GetTheaterInfo
{
    public UserTheaterInfoFormat theaterInfo;
    public long PacketId;
}

