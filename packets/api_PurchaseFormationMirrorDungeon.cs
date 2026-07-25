// /api/PurchaseFormationMirrorDungeon  ReqPacket_PurchaseFormationMirrorDungeon -> ResPacket_PurchaseFormationMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseFormationMirrorDungeon
{
    public List<MirrorDungeonFormationFormat> formation;
    public long PacketId;
}

public class ResPacket_PurchaseFormationMirrorDungeon
{
    public int cost;
    public List<MirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public UserMirrorDungeonShopDataFormat_NEW shopInfo;
    public MirrorDungeonPrevUnitInfoFormat prevUnitInfo;
    public long PacketId;
    public int Cost;
    public List<MirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
    public UserMirrorDungeonShopDataFormat_NEW ShopInfo;
    public MirrorDungeonPrevUnitInfoFormat PrevUnitInfo;
    public int usedcost;
}

