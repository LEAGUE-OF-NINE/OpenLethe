// /api/UpgradeEgoGiftStoryMirrorDungeon  ReqPacket_UpgradeEgoGiftStoryMirrorDungeon -> ResPacket_UpgradeEgoGiftStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpgradeEgoGiftStoryMirrorDungeon
{
    public int egoGiftId;
    public long PacketId;
}

public class ResPacket_UpgradeEgoGiftStoryMirrorDungeon
{
    public int cost;
    public DungeonMapEgoGiftFormat egoGift;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public long PacketId;
    public int Cost;
    public DungeonMapEgoGiftFormat EgoGift;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
}

