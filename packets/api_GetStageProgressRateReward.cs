// /api/GetStageProgressRateReward  ReqPacket_GetStageProgressRateReward -> ResPacket_GetStageProgressRateReward
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetStageProgressRateReward
{
    public int mainchapterid;
    public int subchapterid;
    public int rewardType;
    public long PacketId;
}

public class ResPacket_GetStageProgressRateReward
{
    public Il2CppReferenceArray<Element> rewardList;
    public long PacketId;
    public Il2CppReferenceArray<Element> Reward;
}

