// /api/UpdateAbnormalityLogData  ReqPacket_UpdateAbnormalityLogData -> ResPacket_UpdateAbnormalityLogData
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateAbnormalityLogData
{
    public BattlePassParameterFormat battlePassParameters;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public long PacketId;
}

public class ResPacket_UpdateAbnormalityLogData
{
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public long PacketId;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
}

