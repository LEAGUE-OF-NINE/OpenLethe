// /api/ReportError  ReqPacket_ReportError -> ResPacket_ReportError
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ReportError
{
    public string errorCode;
    public string url;
    public string requestJson;
    public string message;
    public long PacketId;
}

public class ResPacket_ReportError
{
    public long PacketId;
}

