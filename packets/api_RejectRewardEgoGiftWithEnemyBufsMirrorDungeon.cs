// /api/RejectRewardEgoGiftWithEnemyBufsMirrorDungeon  ReqPacket_RejectRewardEgoGiftWithEnemyBufsMirrorDungeon -> ResPacket_RejectRewardEgoGiftWithEnemyBufsMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RejectRewardEgoGiftWithEnemyBufsMirrorDungeon
{
    public long PacketId;
}

public class ResPacket_RejectRewardEgoGiftWithEnemyBufsMirrorDungeon
{
    public List<DungeonMapEgoGiftFormat> egoGifts;
    public List<int> levelAdders;
    public List<RandomDungeonEncounterRewardEventInfoFormat> remainRewardEvent;
    public MirrorDungeonSaveInfoFormat saveinfo;
    public long PacketId;
    public List<DungeonMapEgoGiftFormat> EgoGifts;
    public List<int> LevelAdders;
    public List<RandomDungeonEncounterRewardEventInfoFormat> RemainRewardEvent;
    public MirrorDungeonSaveInfoFormat SaveInfo;
}

