// /api/SellEgoGiftMirrorDungeon  ReqPacket_SellEgoGiftMirrorDungeon -> ResPacket_SellEgoGiftMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SellEgoGiftMirrorDungeon
{
    public int id;
    public long PacketId;
}

public class ResPacket_SellEgoGiftMirrorDungeon
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
}

