// /login/GetInfoOfLinkWith  ReqPacket_GetInfoOfLinkWith -> ResPacket_GetInfoOfLinkWith
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetInfoOfLinkWith
{
    public string targetPublicId;
    public string password;
    public long PacketId;
}

public class ResPacket_GetInfoOfLinkWith
{
    public string details;
    public string state;
    public long PacketId;
}

