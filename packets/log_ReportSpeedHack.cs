// /log/ReportSpeedHack  ReqPacket_ReportSpeedHack -> ResPacket_ReportSpeedHack
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ReportSpeedHack
{
    public string detectedDate;
    public string scene;
    public long PacketId;
}

public class ResPacket_ReportSpeedHack
{
    public long PacketId;
}

