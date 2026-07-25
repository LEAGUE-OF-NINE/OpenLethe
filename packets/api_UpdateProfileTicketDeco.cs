// /api/UpdateProfileTicketDeco  ReqPacket_UpdateProfileTicketDeco -> ResPacket_UpdateProfileTicketDeco
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateProfileTicketDeco
{
    public int leftBorderId;
    public int rightBorderId;
    public int egoBackgroundId;
    public long PacketId;
}

public class ResPacket_UpdateProfileTicketDeco
{
    public int leftBorderId;
    public int rightBorderId;
    public int egoBackgroundId;
    public long PacketId;
}

