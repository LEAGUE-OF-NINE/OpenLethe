// /api/RejectRewardEgoGiftsStoryMirrorDungeon  ReqPacket_RejectRewardEgoGiftsStoryMirrorDungeon -> ResPacket_RejectRewardEgoGiftsStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RejectRewardEgoGiftsStoryMirrorDungeon
{
    public long PacketId;
}

public class ResPacket_RejectRewardEgoGiftsStoryMirrorDungeon
{
    public List<RandomDungeonEncounterRewardEventInfoFormat> remainRewardEvent;
    public long PacketId;
    public List<RandomDungeonEncounterRewardEventInfoFormat> RemainRewardEvent;
}

