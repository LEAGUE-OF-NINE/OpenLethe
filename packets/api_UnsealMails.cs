// /api/UnsealMails  ReqPacket_UnsealMails -> ResPacket_UnsealMails
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UnsealMails
{
    public List<int> mailIds;
    public long PacketId;
}

public class ResPacket_UnsealMails
{
    public List<Element> attachedElements;
    public long PacketId;
}

