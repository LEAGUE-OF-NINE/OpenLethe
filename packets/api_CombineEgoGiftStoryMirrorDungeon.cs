// /api/CombineEgoGiftStoryMirrorDungeon  ReqPacket_CombineEgoGiftStoryMirrorDungeon -> ResPacket_CombineEgoGiftStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_CombineEgoGiftStoryMirrorDungeon
{
    public List<int> materialEgoGiftIds;
    public string keyword;
    public long PacketId;
}

public class ResPacket_CombineEgoGiftStoryMirrorDungeon
{
    public DungeonMapEgoGiftFormat resultEgoGift;
    public bool isSuccess;
    public List<DungeonMapEgoGiftFormat> egoGifts;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public long PacketId;
    public DungeonMapEgoGiftFormat ResultEgoGift;
    public bool IsSuccess;
    public List<DungeonMapEgoGiftFormat> EgoGifts;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
}

