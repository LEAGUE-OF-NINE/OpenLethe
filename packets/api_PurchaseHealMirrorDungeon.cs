// /api/PurchaseHealMirrorDungeon  ReqPacket_PurchaseHealMirrorDungeon -> ResPacket_PurchaseHealMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseHealMirrorDungeon
{
    public int idx;
    public int pid;
    public long PacketId;
}

public class ResPacket_PurchaseHealMirrorDungeon
{
    public int cost;
    public List<MirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public UserMirrorDungeonShopDataFormat_NEW shopInfo;
    public long PacketId;
    public int Cost;
    public List<MirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
    public UserMirrorDungeonShopDataFormat_NEW ShopInfo;
    public int usedcost;
}

