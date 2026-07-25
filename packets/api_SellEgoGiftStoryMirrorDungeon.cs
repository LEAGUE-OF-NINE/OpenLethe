// /api/SellEgoGiftStoryMirrorDungeon  ReqPacket_SellEgoGiftStoryMirrorDungeon -> ResPacket_SellEgoGiftStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SellEgoGiftStoryMirrorDungeon
{
    public int id;
    public long PacketId;
}

public class ResPacket_SellEgoGiftStoryMirrorDungeon
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

