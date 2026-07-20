// /api/ExitStory  ReqPacket_ExitStory -> ResPacket_ExitStory
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitStory
{
    public int mainchapterid;
    public int subchapterid;
    public int nodeid;
    public int stageid;
    public long PacketId;
}

public class ResPacket_ExitStory
{
    public List<Element> rewarditem;
    public List<Element> exrewarditem;
    public long PacketId;
}

