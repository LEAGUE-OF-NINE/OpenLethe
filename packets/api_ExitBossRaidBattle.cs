// /api/ExitBossRaidBattle  ReqPacket_ExitBossRaidBattle -> ResPacket_ExitBossRaidBattle
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitBossRaidBattle
{
    public int raidId;
    public bool isWin;
    public int clearTurn;
    public BossRaidEnemyDataFormat enemy;
    public List<BossRaidEgoStockFormat> egostocks;
    public List<BossRaidStatisticDataFormat> statistics;
    public BattlePassParameterFormat battlePassParameters;
    public List<DanteAbilityUsedLogFormat> danteAbilityUsageLogs;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public long PacketId;
}

public class ResPacket_ExitBossRaidBattle
{
    public BossRaidSaveDataFormat saveInfo;
    public long PacketId;
}

