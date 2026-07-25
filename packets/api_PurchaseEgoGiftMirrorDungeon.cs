// /api/PurchaseEgoGiftMirrorDungeon  ReqPacket_PurchaseEgoGiftMirrorDungeon -> ResPacket_PurchaseEgoGiftMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseEgoGiftMirrorDungeon
{
    public int idx;
    public int egoGiftId;
    public long PacketId;
}

public class ResPacket_PurchaseEgoGiftMirrorDungeon
{
    public int cost;
    public List<DungeonMapEgoGiftFormat> egogifts;
    public UserMirrorDungeonShopDataFormat_NEW shopInfo;
    public List<MirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public long PacketId;
    public int Cost;
    public List<DungeonMapEgoGiftFormat> Egogifts;
    public UserMirrorDungeonShopDataFormat_NEW ShopInfo;
    public List<MirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
    public int usedcost;
}

