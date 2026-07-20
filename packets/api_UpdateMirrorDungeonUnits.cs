// /api/UpdateMirrorDungeonUnits  ReqPacket_UpdateMirrorDungeonUnits -> ResPacket_UpdateMirrorDungeonUnits
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateMirrorDungeonUnits
{
    public List<MirrorDungeonSaveUnitInfoFormat> dungeonunitlist;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_UpdateMirrorDungeonUnits
{
    public long PacketId;
}

