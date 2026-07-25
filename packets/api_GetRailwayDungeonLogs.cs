// /api/GetRailwayDungeonLogs  ReqPacket_GetRailwayDungeonLogs -> ResPacket_GetRailwayDungeonLogs
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetRailwayDungeonLogs
{
    public int dungeonId;
    public long PacketId;
}

public class ResPacket_GetRailwayDungeonLogs
{
    public List<RailwayLogDataFormat> logDatas;
    public long PacketId;
}

