// /api/EndBossRaid  ReqPacket_EndBossRaid -> ResPacket_EndBossRaid
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EndBossRaid
{
    public int raidId;
    public bool reset;
    public long PacketId;
}

public class ResPacket_EndBossRaid
{
    public BossRaidSaveDataFormat saveInfo;
    public BossRaidLogDataFormat logData;
    public List<Element> rewards;
    public long PacketId;
}

