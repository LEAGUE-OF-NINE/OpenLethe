// /api/PurchaseEgoGiftStoryMirrorDungeon  ReqPacket_PurchaseEgoGiftStoryMirrorDungeon -> ResPacket_PurchaseEgoGiftStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseEgoGiftStoryMirrorDungeon
{
    public int idx;
    public long PacketId;
}

public class ResPacket_PurchaseEgoGiftStoryMirrorDungeon
{
    public int cost;
    public List<DungeonMapEgoGiftFormat> egogifts;
    public UserStoryMirrorDungeonShopDataFormat shopInfo;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public long PacketId;
    public int Cost;
    public List<DungeonMapEgoGiftFormat> Egogifts;
    public UserStoryMirrorDungeonShopDataFormat ShopInfo;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
}

