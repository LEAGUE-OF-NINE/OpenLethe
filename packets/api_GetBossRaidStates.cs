// /api/GetBossRaidStates  ReqPacket_GetBossRaidStates -> ResPacket_GetBossRaidStates
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetBossRaidStates
{
    public long PacketId;
}

public class ResPacket_GetBossRaidStates
{
    public List<BossRaidStateFormat> raidStates;
    public long PacketId;
}

public class BossRaidStateFormat
{
    public int raidId;
    public int difficulty;
    public int state;
}

