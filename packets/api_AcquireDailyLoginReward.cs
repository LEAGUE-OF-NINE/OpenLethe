// /api/AcquireDailyLoginReward  ReqPacket_AcquireDailyLoginReward -> ResPacket_AcquireDailyLoginReward
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcquireDailyLoginReward
{
    public int weekid;
    public int id;
    public long PacketId;
}

public class ResPacket_AcquireDailyLoginReward
{
    public List<Element> rewards;
    public long PacketId;
}

