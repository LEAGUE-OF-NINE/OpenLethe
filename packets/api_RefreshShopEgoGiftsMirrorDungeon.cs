// /api/RefreshShopEgoGiftsMirrorDungeon  ReqPacket_RefreshShopEgoGiftsMirrorDungeon -> ResPacket_RefreshShopEgoGiftsMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RefreshShopEgoGiftsMirrorDungeon
{
    public string keyword;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_RefreshShopEgoGiftsMirrorDungeon
{
    public int cost;
    public UserMirrorDungeonShopDataFormat_NEW shopInfo;
    public long PacketId;
    public int Cost;
    public UserMirrorDungeonShopDataFormat_NEW ShopInfo;
    public int usedcost;
}

