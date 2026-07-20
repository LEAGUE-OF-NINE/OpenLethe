// /api/UpdateStoryDungeonUnits  ReqPacket_UpdateStoryDungeonUnits -> ResPacket_UpdateStoryDungeonUnits
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateStoryDungeonUnits
{
    public List<StoryDungeonSaveUnitInfoFormat> dungeonunitlist;
    public long PacketId;
}

public class ResPacket_UpdateStoryDungeonUnits
{
    public long PacketId;
}

