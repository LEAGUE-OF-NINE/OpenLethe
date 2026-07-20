// /api/EnterRailwayDungeon  ReqPacket_EnterRailwayDungeon -> ResPacket_EnterRailwayDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterRailwayDungeon
{
    public int dungeonId;
    public List<RailwayUnitInfoFormat> personalities;
    public long PacketId;
}

public class ResPacket_EnterRailwayDungeon
{
    public RailwayDungeonSaveInfoFormat saveInfo;
    public RailwayNodeDataFormat startNodeData;
    public long PacketId;
    public RailwayDungeonSaveInfoFormat SaveInfo;
    public RailwayNodeDataFormat StartNodeData;
}

