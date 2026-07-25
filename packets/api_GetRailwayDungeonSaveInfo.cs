// /api/GetRailwayDungeonSaveInfo  ReqPacket_GetRailwayDungeonSaveInfo -> ResPacket_GetRailwayDungeonSaveInfo
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetRailwayDungeonSaveInfo
{
    public int dungeonId;
    public long PacketId;
}

public class ResPacket_GetRailwayDungeonSaveInfo
{
    public RailwayDungeonSaveInfoFormat railwaySaveInfo;
    public long PacketId;
}

