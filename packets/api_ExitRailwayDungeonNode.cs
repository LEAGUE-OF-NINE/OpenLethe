// /api/ExitRailwayDungeonNode  ReqPacket_ExitRailwayDungeonNode -> ResPacket_ExitRailwayDungeonNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitRailwayDungeonNode
{
    public int dungeonId;
    public uint nodeid;
    public List<RailwayUnitStatusFormat> unitStatusList;
    public List<RailwayEGOStockFormat> egoSkillStockList;
    public BattlePassParameterFormat battlePassParameters;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public List<RailwayStatisticsDataFormat> statistics;
    public List<DanteAbilityUsedLogFormat> danteAbilityUsageLogs;
    public int clearTurn;
    public bool iswin;
    public SaveDataForRailwayDungeon enemy;
    public RailwayBuffSetInNodeFormat buffsetbyegogift;
    public List<RailwayBattleStateFormat> battleStates;
    public long PacketId;
}

public class ResPacket_ExitRailwayDungeonNode
{
    public RailwayDungeonSaveInfoFormat saveInfo;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public RailwayNodeDataFormat nodeData;
    public List<RailwayNodeDataFormat> updateNodeDatas;
    public long PacketId;
    public RailwayDungeonSaveInfoFormat SaveInfo;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
    public RailwayNodeDataFormat NodeData;
    public List<RailwayNodeDataFormat> UpdateNodeDatas;
}

