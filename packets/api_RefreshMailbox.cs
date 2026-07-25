// /api/RefreshMailbox  ReqPacket_RefreshMailbox -> ResPacket_RefreshMailbox
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RefreshMailbox
{
    public long PacketId;
}

public class ResPacket_RefreshMailbox
{
    public List<MailFormat> initializedMailList;
    public long PacketId;
}

public class MailFormat
{
    public int mail_id;
    public string sent_date;
    public string expiry_date;
    public int content_id;
    public List<Element> attachments;
    public List<string> parameters;
}

