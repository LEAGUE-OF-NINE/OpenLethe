// /api/GetAttendanceState  ReqPacket_GetAttendanceState -> ResPacket_GetAttendanceState
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetAttendanceState
{
    public long PacketId;
}

public class ResPacket_GetAttendanceState
{
    public List<int> rewardState;
    public int consumption;
    public long PacketId;
}

