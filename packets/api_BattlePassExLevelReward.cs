// /api/BattlePassExLevelReward  ReqPacket_BattlePassExLevelReward -> ResPacket_BattlePassExLevelReward
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_BattlePassExLevelReward
{
    public long PacketId;
}

public class ResPacket_BattlePassExLevelReward
{
    public List<Element> resultElements;
    public long PacketId;
}

