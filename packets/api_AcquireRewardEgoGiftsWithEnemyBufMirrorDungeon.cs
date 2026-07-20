// /api/AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon  ReqPacket_AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon -> ResPacket_AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon
{
    public Il2CppStructArray<int> selectIndexList;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon
{
    public Il2CppReferenceArray<DungeonMapEgoGiftFormat> egoGifts;
    public List<RandomDungeonEncounterRewardEventInfoFormat> remainRewardEvent;
    public List<MirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public List<int> levelAdders;
    public MirrorDungeonSaveInfoFormat saveinfo;
    public long PacketId;
    public Il2CppReferenceArray<DungeonMapEgoGiftFormat> EgoGifts;
    public List<RandomDungeonEncounterRewardEventInfoFormat> RemainRewardEvent;
    public List<MirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
    public List<int> LevelAdders;
    public MirrorDungeonSaveInfoFormat SaveInfo;
}

