// /api/AcquireAttendanceReward  ReqPacket_AcquireAttendanceReward -> ResPacket_AcquireAttendanceReward
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcquireAttendanceReward
{
    public int partid;
    public int id;
    public long PacketId;
}

public class ResPacket_AcquireAttendanceReward
{
    public List<int> rewardState;
    public List<Element> rewards;
    public long PacketId;
}

