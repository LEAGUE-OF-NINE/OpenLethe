// /api/ExitThreadDungeonv2  ReqPacket_ExitThreadDungeon -> ResPacket_ExitThreadDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitThreadDungeon
{
    public int isWin;
    public BattlePassParameterFormat battlePassParameters;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public List<DanteAbilityUsedLogFormat> danteAbilityUsageLogs;
    public int progressCount;
    public long PacketId;
}

public class ResPacket_ExitThreadDungeon
{
    public int userExp;
    public List<Element> rewards;
    public ThreadDungeonClearInfoFormat clearInfo;
    public List<Element> consumables;
    public long PacketId;
}

