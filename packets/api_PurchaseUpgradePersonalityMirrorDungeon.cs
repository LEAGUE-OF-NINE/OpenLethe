// /api/PurchaseUpgradePersonalityMirrorDungeon  ReqPacket_PurchaseUpgradePersonalityMirrorDungeon -> ResPacket_PurchaseUpgradePersonalityMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseUpgradePersonalityMirrorDungeon
{
    public int pid;
    public int idx;
    public bool isDetected;
    public bool useStarlight;
    public long PacketId;
}

public class ResPacket_PurchaseUpgradePersonalityMirrorDungeon
{
    public int cost;
    public List<MirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public UserMirrorDungeonShopDataFormat_NEW shopInfo;
    public StarlightInfoFormat starlightInfo;
    public long PacketId;
    public int Cost;
    public List<MirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
    public UserMirrorDungeonShopDataFormat_NEW ShopInfo;
    public StarlightInfoFormat StarlightInfo;
    public int usedcost;
}

