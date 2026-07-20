// /api/EnterBossRaid  ReqPacket_EnterBossRaid -> ResPacket_EnterBossRaid
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterBossRaid
{
    public int raidId;
    public int difficulty;
    public long PacketId;
}

public class ResPacket_EnterBossRaid
{
    public BossRaidSaveDataFormat saveInfo;
    public long PacketId;
}

