// /api/ExitStoryDungeonMapNode  ReqPacket_ExitStoryDungeonMapNode -> ResPacket_ExitStoryDungeonMapNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitStoryDungeonMapNode
{
    public uint noderesult;
    public DungeonChoiceEventSaveDataFormat choiceEventData;
    public List<StoryDungeonSaveUnitInfoFormat> dungeonunitlist;
    public BattlePassParameterFormat battlePassParameters;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public int openedNode;
    public int isupdatedEgoSkillStock;
    public List<DungeonEgoSkillStockFormat> egoSkillStockList;
    public List<DungeonMapEgoGiftFormat> updatedEgoGifts;
    public List<DungeonStatisticsDataFormat> statistics;
    public List<DanteAbilityUsedLogFormat> danteAbilityUsageLogs;
    public int curEventEncounterId;
    public int clearTurn;
    public int subchapterid;
    public ContinueUsageLogFormat continueUsageLog;
    public long PacketId;
}

public class ResPacket_ExitStoryDungeonMapNode
{
    public StoryDungeonSaveInfoFormat saveInfo;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public List<DungeonMapEgoGiftFormat> acquiredEgogifts;
    public uint isAllDie;
    public long PacketId;
    public StoryDungeonSaveInfoFormat SaveInfo;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
    public List<DungeonMapEgoGiftFormat> AcquiredEgogifts;
    public uint IsAllDie;
}

