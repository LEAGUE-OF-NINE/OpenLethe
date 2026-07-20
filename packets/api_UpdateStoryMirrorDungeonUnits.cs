// /api/UpdateStoryMirrorDungeonUnits  ReqPacket_UpdateStoryMirrorDungeonUnits -> ResPacket_UpdateStoryMirrorDungeonUnits
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateStoryMirrorDungeonUnits
{
    public List<StoryMirrorDungeonSaveUnitInfoFormat> dungeonunitlist;
    public long PacketId;
}

public class ResPacket_UpdateStoryMirrorDungeonUnits
{
    public long PacketId;
}

