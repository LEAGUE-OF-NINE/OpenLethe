// /log/ReportModifiedHash  ReqPacket_ReportModifiedHashCatalog -> ResPacket_ReportModifiedHashCatalog
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ReportModifiedHashCatalog
{
    public string platform;
    public string hashvalue;
    public long PacketId;
}

public class ResPacket_ReportModifiedHashCatalog
{
    public long PacketId;
}

