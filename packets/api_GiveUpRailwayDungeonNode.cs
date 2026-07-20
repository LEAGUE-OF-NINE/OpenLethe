// /api/GiveUpRailwayDungeonNode  ReqPacket_GiveUpRailwayDungeonNode -> ResPacket_GiveUpRailwayDungeonNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GiveUpRailwayDungeonNode
{
    public int dungeonId;
    public uint nodeid;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public long PacketId;
}

public class ResPacket_GiveUpRailwayDungeonNode
{
    public RailwayDungeonSaveInfoFormat saveInfo;
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public RailwayNodeDataFormat nodeData;
    public long PacketId;
    public RailwayDungeonSaveInfoFormat SaveInfo;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
    public RailwayNodeDataFormat NodeData;
}

