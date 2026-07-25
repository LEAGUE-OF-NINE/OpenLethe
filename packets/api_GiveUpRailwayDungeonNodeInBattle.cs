// /api/GiveUpRailwayDungeonNodeInBattle  ReqPacket_GiveUpRailwayDungeonNodeInBattle -> ResPacket_GiveUpRailwayDungeonNodeInBattle
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GiveUpRailwayDungeonNodeInBattle
{
    public int dungeonid;
    public int nodeid;
    public BattlePassParameterFormat battlePassParameters;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public long PacketId;
}

public class ResPacket_GiveUpRailwayDungeonNodeInBattle
{
    public RailwayNodeDataFormat nodeData;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public long PacketId;
    public RailwayNodeDataFormat NodeData;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
}

