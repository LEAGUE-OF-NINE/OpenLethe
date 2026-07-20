// /api/ExitRailwayDungeonRestNode  ReqPacket_ExitRailwayDungeonRestNode -> ResPacket_ExitRailwayDungeonRestNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitRailwayDungeonRestNode
{
    public int dungeonId;
    public int nodeid;
    public List<RailwayUnitInfoFormat> personalities;
    public long PacketId;
}

public class ResPacket_ExitRailwayDungeonRestNode
{
    public RailwayDungeonSaveInfoFormat saveInfo;
    public List<int> deletedNodeIds;
    public RailwayNodeDataFormat nodeData;
    public long PacketId;
    public RailwayDungeonSaveInfoFormat SaveInfo;
    public List<int> DeletedNodeIds;
    public RailwayNodeDataFormat NodeData;
}

