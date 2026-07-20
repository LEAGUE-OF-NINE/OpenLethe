// /api/SelectRailwayDungeonBuff  ReqPacket_SelectRailwayDungeonBuff -> ResPacket_SelectRailwayDungeonBuff
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SelectRailwayDungeonBuff
{
    public int dungeonId;
    public List<RailwayBuffSetRequestFormat> selectedBuffs;
    public long PacketId;
}

public class ResPacket_SelectRailwayDungeonBuff
{
    public RailwayDungeonSaveInfoFormat saveInfo;
    public RailwayNodeDataFormat nodeData;
    public long PacketId;
    public RailwayDungeonSaveInfoFormat SaveInfo;
    public RailwayNodeDataFormat NodeData;
}

public class RailwayBuffSetRequestFormat
{
    public int setId;
    public int buffId;
    public int targetId;
}

