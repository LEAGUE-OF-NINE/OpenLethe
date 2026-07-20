// /api/PurchaseHealStoryMirrorDungeon  ReqPacket_PurchaseHealStoryMirrorDungeon -> ResPacket_PurchaseHealStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseHealStoryMirrorDungeon
{
    public int idx;
    public int pid;
    public long PacketId;
}

public class ResPacket_PurchaseHealStoryMirrorDungeon
{
    public int cost;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public UserStoryMirrorDungeonShopDataFormat shopInfo;
    public long PacketId;
    public int Cost;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
    public UserStoryMirrorDungeonShopDataFormat ShopInfo;
}

