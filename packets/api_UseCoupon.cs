// /api/UseCoupon  ReqPacket_UseCoupon -> ResPacket_UseCoupon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UseCoupon
{
    public string code;
    public long PacketId;
}

public class ResPacket_UseCoupon
{
    public int state;
    public List<Element> rewards;
    public string backoffdate;
    public DateUtil _backoffDate;
    public int backoffduration;
    public long PacketId;
    public int State;
    public List<Element> Rewards;
    public DateUtil BackOffDate;
    public int Backoffduration;
}

