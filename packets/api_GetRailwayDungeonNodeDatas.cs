// /api/GetRailwayDungeonNodeDatas  ReqPacket_GetRailwayDungeonNodeDatas -> ResPacket_GetRailwayDungeonNodeDatas
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetRailwayDungeonNodeDatas
{
    public int dungeonId;
    public long PacketId;
}

public class ResPacket_GetRailwayDungeonNodeDatas
{
    public List<RailwayNodeDataFormat> nodeDatas;
    public long PacketId;
}

