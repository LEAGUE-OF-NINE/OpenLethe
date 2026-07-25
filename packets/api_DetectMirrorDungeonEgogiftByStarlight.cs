// /api/DetectMirrorDungeonEgogiftByStarlight  ReqPacket_DetectMirrorDungeonEgogiftByStarlight -> ResPacket_DetectMirrorDungeonEgogiftByStarlight
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_DetectMirrorDungeonEgogiftByStarlight
{
    public List<int> egogiftIds;
    public long PacketId;
}

public class ResPacket_DetectMirrorDungeonEgogiftByStarlight
{
    public MirrorDungeonCurrentInfoFormat currentInfo;
    public long PacketId;
    public MirrorDungeonCurrentInfoFormat CurrentInfo;
}

