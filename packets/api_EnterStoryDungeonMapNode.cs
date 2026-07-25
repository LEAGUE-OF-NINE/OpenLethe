// /api/EnterStoryDungeonMapNode  ReqPacket_EnterStoryDungeonMapNode -> ResPacket_EnterStoryDungeonMapNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterStoryDungeonMapNode
{
    public int floornumber;
    public int sectornumber;
    public int nodeid;
    public List<int> abnormalityids;
    public List<int> participatedPIds;
    public int continueVersion;
    public long PacketId;
}

public class ResPacket_EnterStoryDungeonMapNode
{
    public DungeonMapNodeFormat node;
    public int nr;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public Il2CppReferenceArray<ChoiceEventLogFormat> cels;
    public long PacketId;
    public DungeonMapNodeFormat Node;
    public int NodeResult;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
    public Il2CppReferenceArray<ChoiceEventLogFormat> Cels;
}

