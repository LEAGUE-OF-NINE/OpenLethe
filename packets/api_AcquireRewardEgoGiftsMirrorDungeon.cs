// /api/AcquireRewardEgoGiftsMirrorDungeon  ReqPacket_AcquireRewardEgoGiftsMirrorDungeon -> ResPacket_AcquireRewardEgoGiftsMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcquireRewardEgoGiftsMirrorDungeon
{
    public List<int> selectIndexList;
    public bool isStartBufEgoGift;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_AcquireRewardEgoGiftsMirrorDungeon
{
    public List<DungeonMapEgoGiftFormat> egoGifts;
    public List<RandomDungeonEncounterRewardEventInfoFormat> remainRewardEvent;
    public List<MirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public MirrorDungeonSaveInfoFormat saveinfo;
    public long PacketId;
    public List<DungeonMapEgoGiftFormat> EgoGifts;
    public List<RandomDungeonEncounterRewardEventInfoFormat> RemainRewardEvent;
    public List<MirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
    public MirrorDungeonSaveInfoFormat SaveInfo;
}

