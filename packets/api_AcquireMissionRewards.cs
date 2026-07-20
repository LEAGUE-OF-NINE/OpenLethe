// /api/AcquireMissionRewards  ReqPacket_AcquireMissionRewards -> ResPacket_AcquireMissionRewards
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcquireMissionRewards
{
    public List<int> missionIds;
    public long PacketId;
}

public class ResPacket_AcquireMissionRewards
{
    public Il2CppReferenceArray<Element> acquiredElements;
    public long PacketId;
    public Il2CppReferenceArray<Element> AcquiredElements;
}

