// /api/AcquireMirrorDungeonBattleReward  ReqPacket_AcquireMirrorDungeonBattleReward -> ResPacket_AcquireMirrorDungeonBattleReward
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcquireMirrorDungeonBattleReward
{
    public List<int> selectIndexList;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_AcquireMirrorDungeonBattleReward
{
    public MirrorDungeonSaveInfoFormat saveinfo;
    public long PacketId;
    public MirrorDungeonSaveInfoFormat SaveInfoFormat;
}

