// /api/EnterExpDungeonv2  ReqPacket_EnterExpDungeon -> ResPacket_EnterExpDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterExpDungeon
{
    public int dungeonid;
    public int progressCount;
    public long PacketId;
}

public class ResPacket_EnterExpDungeon
{
    public int isclear;
    public long PacketId;
}

