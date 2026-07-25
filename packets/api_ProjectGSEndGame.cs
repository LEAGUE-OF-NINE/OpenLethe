// /api/ProjectGSEndGame  ReqPacket_ProjectGSEndGame -> ResPacket_ProjectGSEndGame
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ProjectGSEndGame
{
    public long PacketId;
}

public class ResPacket_ProjectGSEndGame
{
    public ProjectGSFinalStats finalStats;
    public int addexptouser;
    public List<Element> rewarditem;
    public List<Element> exrewarditem;
    public List<Element> firstrewarditem;
    public long PacketId;
}

