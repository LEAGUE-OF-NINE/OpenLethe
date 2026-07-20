// /api/EnterStageBattle  ReqPacket_EnterStageBattle -> ResPacket_EnterStageBattle
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterStageBattle
{
    public int mainchapterid;
    public int subchapterid;
    public int nodeid;
    public int stageid;
    public List<int> abnormalityids;
    public int continueVersion;
    public long PacketId;
}

public class ResPacket_EnterStageBattle
{
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public long PacketId;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
}

