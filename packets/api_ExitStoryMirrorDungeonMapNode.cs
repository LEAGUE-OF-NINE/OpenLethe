// /api/ExitStoryMirrorDungeonMapNode  ReqPacket_ExitStoryMirrorDungeonMapNode -> ResPacket_ExitStoryMirrorDungeonMapNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitStoryMirrorDungeonMapNode
{
    public DungeonMapNodeFormat currentnode;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> dungeonunitlist;
    public int noderesult;
    public DungeonChoiceEventSaveDataFormat choiceEventData;
    public BattlePassParameterFormat battlePassParameters;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public int isupdatedEgoSkillStock;
    public List<DungeonEgoSkillStockFormat> egoSkillStockList;
    public List<DungeonMapEgoGiftFormat> updatedEgoGifts;
    public List<DungeonStatisticsDataFormat> statistics;
    public List<DanteAbilityUsedLogFormat> danteAbilityUsageLogs;
    public int clearTurn;
    public long PacketId;
}

public class ResPacket_ExitStoryMirrorDungeonMapNode
{
    public StoryMirrorDungeonCurrentInfoFormat currentInfo;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public long PacketId;
    public StoryMirrorDungeonCurrentInfoFormat CurrentInfo;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
}

