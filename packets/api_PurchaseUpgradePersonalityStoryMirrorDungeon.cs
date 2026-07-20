// /api/PurchaseUpgradePersonalityStoryMirrorDungeon  ReqPacket_PurchaseUpgradePersonalityStoryMirrorDungeon -> ResPacket_PurchaseUpgradePersonalityStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseUpgradePersonalityStoryMirrorDungeon
{
    public int idx;
    public long PacketId;
}

public class ResPacket_PurchaseUpgradePersonalityStoryMirrorDungeon
{
    public int cost;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public UserStoryMirrorDungeonShopDataFormat shopInfo;
    public long PacketId;
    public int Cost;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
    public UserStoryMirrorDungeonShopDataFormat ShopInfo;
}

