// /api/RefreshShopEgoGiftsStoryMirrorDungeon  ReqPacket_RefreshShopEgoGiftsStoryMirrorDungeon -> ResPacket_RefreshShopEgoGiftsStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RefreshShopEgoGiftsStoryMirrorDungeon
{
    public string keyword;
    public long PacketId;
}

public class ResPacket_RefreshShopEgoGiftsStoryMirrorDungeon
{
    public int cost;
    public UserStoryMirrorDungeonShopDataFormat shopInfo;
    public long PacketId;
    public int Cost;
    public UserStoryMirrorDungeonShopDataFormat ShopInfo;
}

