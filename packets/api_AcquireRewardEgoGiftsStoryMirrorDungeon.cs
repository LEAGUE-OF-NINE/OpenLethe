// /api/AcquireRewardEgoGiftsStoryMirrorDungeon  ReqPacket_AcquireRewardEgoGiftsStoryMirrorDungeon -> ResPacket_AcquireRewardEgoGiftsStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcquireRewardEgoGiftsStoryMirrorDungeon
{
    public List<int> selectIndexList;
    public long PacketId;
}

public class ResPacket_AcquireRewardEgoGiftsStoryMirrorDungeon
{
    public List<DungeonMapEgoGiftFormat> egoGifts;
    public List<RandomDungeonEncounterRewardEventInfoFormat> remainRewardEvent;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public long PacketId;
    public List<DungeonMapEgoGiftFormat> EgoGifts;
    public List<RandomDungeonEncounterRewardEventInfoFormat> RemainRewardEvent;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
}

