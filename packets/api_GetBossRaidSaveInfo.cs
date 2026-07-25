// /api/GetBossRaidSaveInfo  ReqPacket_GetBossRaidSaveInfo -> ResPacket_GetBossRaidSaveInfo
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetBossRaidSaveInfo
{
    public int raidId;
    public long PacketId;
}

public class ResPacket_GetBossRaidSaveInfo
{
    public BossRaidSaveDataFormat saveInfo;
    public List<BossRaidPartyDetailFormat> partyDatas;
    public BossRaidLogDataFormat logData;
    public long PacketId;
}

