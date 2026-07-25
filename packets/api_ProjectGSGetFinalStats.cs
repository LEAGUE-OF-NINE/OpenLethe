// /api/ProjectGSGetFinalStats  ReqPacket_ProjectGSGetFinalStats -> ResPacket_ProjectGSGetFinalStats
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ProjectGSGetFinalStats
{
    public long PacketId;
}

public class ResPacket_ProjectGSGetFinalStats
{
    public ProjectGSFinalStats finalStats;
    public long PacketId;
}

