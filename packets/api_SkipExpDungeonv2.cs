// /api/SkipExpDungeonv2  ReqPacket_SkipExpDungeon -> ResPacket_SkipExpDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SkipExpDungeon
{
    public int dungeonid;
    public int progressCount;
    public long PacketId;
}

public class ResPacket_SkipExpDungeon
{
    public int userExp;
    public List<Element> rewards;
    public List<Element> consumables;
    public long PacketId;
}

