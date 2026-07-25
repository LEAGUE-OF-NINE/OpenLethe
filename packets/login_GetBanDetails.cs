// /login/GetBanDetails  ReqPacket_GetBanDetails -> ResPacket_GetBanDetails
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetBanDetails
{
    public long PacketId;
}

public class ResPacket_GetBanDetails
{
    public string startDate;
    public DateUtil _startDate;
    public string endDate;
    public DateUtil _endDate;
    public string reason;
    public long PacketId;
    public DateUtil StartDate;
    public DateUtil EndDate;
}

