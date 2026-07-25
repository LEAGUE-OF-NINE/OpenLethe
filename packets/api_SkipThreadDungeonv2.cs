// /api/SkipThreadDungeonv2  ReqPacket_SkipThreadDungeon -> ResPacket_SkipThreadDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SkipThreadDungeon
{
    public int dungeonid;
    public int dungeonlevel;
    public int progressCount;
    public long PacketId;
}

public class ResPacket_SkipThreadDungeon
{
    public int userExp;
    public List<Element> rewards;
    public List<Element> consumables;
    public long PacketId;
}

