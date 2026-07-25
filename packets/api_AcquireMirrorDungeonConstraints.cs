// /api/AcquireMirrorDungeonConstraints  ReqPacket_AcquireMirrorDungeonConstraints -> ResPacket_AcquireMirrorDungeonConstraints
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcquireMirrorDungeonConstraints
{
    public List<int> selectIdxList;
    public long PacketId;
}

public class ResPacket_AcquireMirrorDungeonConstraints
{
    public MirrorDungeonCurrentInfoFormat currentInfo;
    public long PacketId;
    public MirrorDungeonCurrentInfoFormat CurrentInfo;
}

