// /api/AcquireRailwayDungeonReward  ReqPacket_AcquireRailwayDungeonReward -> ResPacket_AcquireRailwayDungeonReward
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcquireRailwayDungeonReward
{
    public int dungeonId;
    public long PacketId;
}

public class ResPacket_AcquireRailwayDungeonReward
{
    public RailwayDungeonSaveInfoFormat saveInfo;
    public List<Element> rewardList;
    public long PacketId;
    public RailwayDungeonSaveInfoFormat SaveInfo;
    public List<Element> RewardList;
}

