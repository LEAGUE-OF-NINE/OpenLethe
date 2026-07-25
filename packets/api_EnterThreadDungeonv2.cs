// /api/EnterThreadDungeonv2  ReqPacket_EnterThreadDungeon -> ResPacket_EnterThreadDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterThreadDungeon
{
    public int dungeonid;
    public int level;
    public List<int> abnormalityids;
    public int progressCount;
    public long PacketId;
}

public class ResPacket_EnterThreadDungeon
{
    public int isClear;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public int progressCount;
    public long PacketId;
}

