// /log/GetMailLogAll  ReqPacket_GetMailLogAll -> ResPacket_GetMailLogAll
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetMailLogAll
{
    public long PacketId;
}

public class ResPacket_GetMailLogAll
{
    public List<MailLog> mailLogs;
    public long PacketId;
}

public class MailLog
{
    public int maillog_id;
    public string sent_date;
    public DateUtil _sentDate;
    public int content_id;
    public List<Element> attachments;
    public string unsealed_date;
    public DateUtil _unsealedDate;
    public List<string> parameters;
    public int MailLogId;
    public DateUtil SentDate;
    public int ContentId;
    public List<Element> Attachments;
    public DateUtil UnsealedDate;
}

