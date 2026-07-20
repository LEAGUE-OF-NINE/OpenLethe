// /api/ExitMirrorDungeonMapNode  ReqPacket_ExitMirrorDungeonMapNode -> ResPacket_ExitMirrorDungeonMapNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitMirrorDungeonMapNode
{
    public DungeonMapNodeFormat currentnode;
    public List<MirrorDungeonSaveUnitInfoFormat> dungeonunitlist;
    public int noderesult;
    public DungeonChoiceEventSaveDataFormat choiceEventData;
    public BattlePassParameterFormat battlePassParameters;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public int isupdatedEgoSkillStock;
    public List<DungeonEgoSkillStockFormat> egoSkillStockList;
    public List<DungeonMapEgoGiftFormat> updatedEgoGifts;
    public List<DungeonStatisticsDataFormat> statistics;
    public int usedDanteAbilityCount;
    public List<DanteAbilityUsedLogFormat> danteAbilityUsageLogs;
    public int battleStatus;
    public int clearTurn;
    public List<MissionConditionContextFormat> missionConditionContexts;
    public MirrorDungeonInputAnalytics inputAnalytics;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_ExitMirrorDungeonMapNode
{
    public MirrorDungeonCurrentInfoFormat currentInfo;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public long PacketId;
    public MirrorDungeonCurrentInfoFormat CurrentInfo;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
}

public class MirrorDungeonInputAnalytics
{
    public float duration;
    public int mouseReal;
    public int mouseSynthetic;
    public float mouseSyntheticRatio;
    public int keyReal;
    public int keySynthetic;
    public float keySyntheticRatio;
    public string platform;
}

