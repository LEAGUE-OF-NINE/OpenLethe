// /api/EnterRailwayDungeonNode  ReqPacket_EnterRailwayDungeonNode -> ResPacket_EnterRailwayDungeonNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterRailwayDungeonNode
{
    public int dungeonId;
    public int nodeid;
    public List<int> abnormalityids;
    public List<int> participatedPIds;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public List<RailwayUnitInfoFormat> personalities;
    public long PacketId;
}

public class ResPacket_EnterRailwayDungeonNode
{
    public uint nodeid;
    public List<int> deletedNodeIds;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public List<RailwayUnitStatusFormat> prevStatusData;
    public List<RailwayEGOStockFormat> prevEgoStockData;
    public SaveDataForRailwayDungeon prevEnemyData;
    public int prevClearNodeId;
    public int currentNodeId;
    public Il2CppReferenceArray<RailwayBuffSetInNodeFormat> buffsetsbyegogift;
    public long PacketId;
    public uint NodeId;
    public List<int> DeletedNodeIds;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
    public List<RailwayUnitStatusFormat> PrevStatusData;
    public List<RailwayEGOStockFormat> PrevEgoStockData;
    public SaveDataForRailwayDungeon PrevEnemyData;
    public int PrevClearNodeId;
    public int CurrentNodeId;
    public Il2CppReferenceArray<RailwayBuffSetInNodeFormat> Buffsetsbyegogift;
}

