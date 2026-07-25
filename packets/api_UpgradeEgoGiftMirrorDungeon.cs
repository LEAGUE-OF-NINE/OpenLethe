// /api/UpgradeEgoGiftMirrorDungeon  ReqPacket_UpgradeEgoGiftMirrorDungeon -> ResPacket_UpgradeEgoGiftMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpgradeEgoGiftMirrorDungeon
{
    public int egoGiftId;
    public int from;
    public int to;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_UpgradeEgoGiftMirrorDungeon
{
    public int cost;
    public DungeonMapEgoGiftFormat egoGift;
    public List<MirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public long PacketId;
    public int Cost;
    public DungeonMapEgoGiftFormat EgoGift;
    public List<MirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
    public int usedcost;
}

