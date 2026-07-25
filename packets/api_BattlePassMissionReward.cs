// /api/BattlePassMissionReward  ReqPacket_BattlePassMissionReward -> ResPacket_BattlePassMissionReward
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_BattlePassMissionReward
{
    public int missionType;
    public int missionId;
    public long PacketId;
}

public class ResPacket_BattlePassMissionReward
{
    public long PacketId;
}

