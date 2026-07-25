// /api/GetDailyLoginState  ReqPacket_GetDailyLoginState -> ResPacket_GetDailyLoginState
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetDailyLoginState
{
    public long PacketId;
}

public class ResPacket_GetDailyLoginState
{
    public int weekid;
    public int id;
    public List<DailyLoginRewardStateFormat> rewardstates;
    public long PacketId;
}

