// /api/BattlePassReward_ALL  ReqPacket_BattlePassRewardAll -> ResPacket_BattlePassRewardAll
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_BattlePassRewardAll
{
    public long PacketId;
}

public class ResPacket_BattlePassRewardAll
{
    public List<Element> resultElements;
    public long PacketId;
}

