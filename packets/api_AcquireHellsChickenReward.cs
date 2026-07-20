// /api/AcquireHellsChickenReward  ReqPacket_AcquireHellsChickenReward -> ResPacket_AcquireHellsChickenReward
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcquireHellsChickenReward
{
    public int rewardId;
    public long PacketId;
}

public class ResPacket_AcquireHellsChickenReward
{
    public List<int> rewardState;
    public List<Element> rewards;
    public long PacketId;
}

