// /api/ClaimEventReward_ALL  ReqPacket_ClaimEventReward_ALL -> ResPacket_ClaimEventReward_ALL
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ClaimEventReward_ALL
{
    public int eventId;
    public int count;
    public long PacketId;
}

public class ResPacket_ClaimEventReward_ALL
{
    public Il2CppReferenceArray<Element> acquiredElements;
    public long PacketId;
    public Il2CppReferenceArray<Element> AcquiredElements;
}

