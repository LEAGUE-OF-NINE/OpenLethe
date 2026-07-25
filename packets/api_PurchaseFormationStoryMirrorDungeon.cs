// /api/PurchaseFormationStoryMirrorDungeon  ReqPacket_PurchaseFormationStoryMirrorDungeon -> ResPacket_PurchaseFormationStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseFormationStoryMirrorDungeon
{
    public List<MirrorDungeonFormationFormat> formation;
    public long PacketId;
}

public class ResPacket_PurchaseFormationStoryMirrorDungeon
{
    public int cost;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public UserStoryMirrorDungeonShopDataFormat shopInfo;
    public MirrorDungeonPrevUnitInfoFormat prevUnitInfo;
    public long PacketId;
    public int Cost;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
    public UserStoryMirrorDungeonShopDataFormat ShopInfo;
    public MirrorDungeonPrevUnitInfoFormat PrevUnitInfo;
}

