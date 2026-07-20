// /api/ClaimEventReward  ReqPacket_ClaimEventReward -> ResPacket_ClaimEventReward
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ClaimEventReward
{
    public int eventId;
    public int eventRewardId;
    public int count;
    public long PacketId;
}

public class ResPacket_ClaimEventReward
{
    public Il2CppReferenceArray<Element> acquiredElements;
    public long PacketId;
    public Il2CppReferenceArray<Element> AcquiredElements;
}

