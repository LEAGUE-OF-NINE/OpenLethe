// /api/StartBossRaidBattle  ReqPacket_StartBossRaidBattle -> ResPacket_StartBossRaidBattle
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_StartBossRaidBattle
{
    public int raidId;
    public List<BossRaidPartyPersonalityFormat> personalities;
    public long PacketId;
}

public class ResPacket_StartBossRaidBattle
{
    public BossRaidEnemyDataFormat enemy;
    public List<BossRaidEgoStockFormat> egostocks;
    public long PacketId;
}

public class BossRaidPartyPersonalityFormat
{
    public int pid;
    public int pord;
}

