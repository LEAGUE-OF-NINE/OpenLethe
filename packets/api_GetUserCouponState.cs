// /api/GetUserCouponState  ReqPacket_GetCouponFailState -> ResPacket_GetCouponFailState
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetCouponFailState
{
    public long PacketId;
}

public class ResPacket_GetCouponFailState
{
    public bool ispossiblestate;
    public string backoffdate;
    public DateUtil _backoffDate;
    public int backoffduration;
    public long PacketId;
    public bool IsPossibleState;
    public DateUtil BackOffDate;
    public int Backoffduration;
}

