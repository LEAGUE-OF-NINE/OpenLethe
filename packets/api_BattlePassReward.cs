// /api/BattlePassReward  ReqPacket_BattlePassReward -> ResPacket_BattlePassReward
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_BattlePassReward
{
    public int level;
    public long PacketId;
}

public class ResPacket_BattlePassReward
{
    public List<Element> resultElements;
    public long PacketId;
}

