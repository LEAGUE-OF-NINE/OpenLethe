// /api/ExitStageBattle  ReqPacket_ExitStageBattle -> ResPacket_ExitStageBattle
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitStageBattle
{
    public int mainchapterid;
    public int subchapterid;
    public int nodeid;
    public int stageid;
    public bool iswin;
    public string battleLogUuid;
    public int battleLogRetryCount;
    public bool battleLogSendSuccess;
    public int turn;
    public int formationid;
    public BattlePassParameterFormat battlePassParameters;
    public int supportCharacterId;
    public int supportPersonalityId;
    public List<int> supportEgoIds;
    public bool supportParticipate;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public List<MissionConditionContextFormat> missionConditionContexts;
    public int usedDanteAbilityCount;
    public List<DanteAbilityUsedLogFormat> danteAbilityUsageLogs;
    public int clearStatus;
    public List<UserStageStatisticPersonalityFormat> unitInfo;
    public ContinueUsageLogFormat continueUsageLog;
    public List<StageDamageStatisticFormat> damageStatistics;
    public long PacketId;
}

public class ResPacket_ExitStageBattle
{
    public int stageid;
    public bool iswin;
    public int cleartype;
    public int addexptouser;
    public List<StagePersonalityInfoFormat> personalityinfos;
    public List<Element> expticket;
    public List<Element> rewarditem;
    public List<Element> exrewarditem;
    public List<Element> firstrewarditem;
    public Element givebackstaminabyDefeat;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public long PacketId;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
}

public class StageDamageStatisticFormat
{
    public int id;
    public int gd;
    public int rd;
}

