// /api/GetAbnormalityLogData  ReqPacket_GetAbnormalityLogData -> ResPacket_GetAbnormalityLogData
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetAbnormalityLogData
{
    public Il2CppStructArray<int> abnormalityIds;
    public long PacketId;
}

public class ResPacket_GetAbnormalityLogData
{
    public List<AbnormalityUnlockInformationFormat> logdatas;
    public long PacketId;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
}

