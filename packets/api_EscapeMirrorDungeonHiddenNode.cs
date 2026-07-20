// /api/EscapeMirrorDungeonHiddenNode  ReqPacket_EscapeMirrorDungeonHiddenNode -> ResPacket_EscapeMirrorDungeonHiddenNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EscapeMirrorDungeonHiddenNode
{
    public DungeonMapNodeFormat currentnode;
    public long PacketId;
}

public class ResPacket_EscapeMirrorDungeonHiddenNode
{
    public MirrorDungeonCurrentInfoFormat currentInfo;
    public long PacketId;
    public MirrorDungeonCurrentInfoFormat CurrentInfo;
}

