// /api/RejectRewardEgoGiftsMirrorDungeon  ReqPacket_RejectRewardEgoGiftsMirrorDungeon -> ResPacket_RejectRewardEgoGiftsMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RejectRewardEgoGiftsMirrorDungeon
{
    public bool isStartBufEgoGift;
    public long PacketId;
}

public class ResPacket_RejectRewardEgoGiftsMirrorDungeon
{
    public List<RandomDungeonEncounterRewardEventInfoFormat> remainRewardEvent;
    public MirrorDungeonSaveInfoFormat saveinfo;
    public long PacketId;
    public List<RandomDungeonEncounterRewardEventInfoFormat> RemainRewardEvent;
    public MirrorDungeonSaveInfoFormat SaveInfo;
}

