// /api/GetRailwayDungeonNodeAndLogAll  ReqPacket_GetRailwayDungeonNodeAndLogAll -> ResPacket_GetRailwayDungeonNodeAndLogAll
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetRailwayDungeonNodeAndLogAll
{
    public int dungeonId;
    public long PacketId;
}

public class ResPacket_GetRailwayDungeonNodeAndLogAll
{
    public RailwayDungeonSaveInfoFormat railwaySaveInfo;
    public List<RailwayNodeDataFormat> nodeDatas;
    public List<RailwayLogDataFormat> logDatas;
    public long PacketId;
}

