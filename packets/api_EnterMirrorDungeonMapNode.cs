// /api/EnterMirrorDungeonMapNode  ReqPacket_EnterMirrorDungeonMapNode -> ResPacket_EnterMirrorDungeonMapNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterMirrorDungeonMapNode
{
    public DungeonMapNodeFormat currentnode;
    public Il2CppStructArray<int> abnormalityids;
    public List<int> participatedPIds;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_EnterMirrorDungeonMapNode
{
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public List<int> passingNodeIds;
    public DungeonMapNodeFormat currentNode;
    public UserMirrorDungeonShopDataFormat_NEW shopInfo;
    public List<DungeonMapEgoGiftFormat> egogifts;
    public List<MirrorDungeonPrevUnitInfoFormat> prevdul;
    public List<int> preves;
    public int nr;
    public Il2CppReferenceArray<ChoiceEventLogFormat> cels;
    public int cost;
    public bool changedHiddenNode;
    public RandomDungeonMapFormat dungeonMap;
    public long PacketId;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
    public List<int> PassingNodeIds;
    public DungeonMapNodeFormat CurrentNode;
    public UserMirrorDungeonShopDataFormat_NEW ShopInfo;
    public List<DungeonMapEgoGiftFormat> Egogifts;
    public List<MirrorDungeonPrevUnitInfoFormat> PrevDul;
    public List<int> PrevEgos;
    public int NodeResult;
    public Il2CppReferenceArray<ChoiceEventLogFormat> Cels;
    public int Cost;
    public bool ChangedHiddenNode;
    public RandomDungeonMapFormat DungeonMap;
}

