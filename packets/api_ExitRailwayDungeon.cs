// /api/ExitRailwayDungeon  ReqPacket_ExitRailwayDungeon -> ResPacket_ExitRailwayDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitRailwayDungeon
{
    public int dungeonId;
    public bool isClear;
    public long PacketId;
}

public class ResPacket_ExitRailwayDungeon
{
    public bool isclear;
    public RailwayDungeonSaveInfoFormat saveInfo;
    public RailwayLogDataFormat currentLog;
    public List<Element> rewards;
    public long PacketId;
    public bool IsClear;
    public RailwayDungeonSaveInfoFormat SaveInfo;
    public RailwayLogDataFormat CurrentLog;
    public List<Element> Rewards;
}

