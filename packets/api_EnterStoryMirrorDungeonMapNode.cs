// /api/EnterStoryMirrorDungeonMapNode  ReqPacket_EnterStoryMirrorDungeonMapNode -> ResPacket_EnterStoryMirrorDungeonMapNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterStoryMirrorDungeonMapNode
{
    public DungeonMapNodeFormat currentnode;
    public Il2CppStructArray<int> abnormalityids;
    public List<int> participatedPIds;
    public long PacketId;
}

public class ResPacket_EnterStoryMirrorDungeonMapNode
{
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public List<int> passingNodeIds;
    public DungeonMapNodeFormat currentNode;
    public UserStoryMirrorDungeonShopDataFormat shopInfo;
    public List<DungeonMapEgoGiftFormat> egogifts;
    public List<MirrorDungeonPrevUnitInfoFormat> prevdul;
    public List<int> preves;
    public Il2CppReferenceArray<ChoiceEventLogFormat> cels;
    public long PacketId;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
    public List<int> PassingNodeIds;
    public DungeonMapNodeFormat CurrentNode;
    public UserStoryMirrorDungeonShopDataFormat ShopInfo;
    public List<DungeonMapEgoGiftFormat> Egogifts;
    public List<MirrorDungeonPrevUnitInfoFormat> PrevDul;
    public List<int> PrevEgos;
    public Il2CppReferenceArray<ChoiceEventLogFormat> Cels;
}

