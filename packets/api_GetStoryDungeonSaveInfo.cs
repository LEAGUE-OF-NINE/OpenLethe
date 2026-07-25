// /api/GetStoryDungeonSaveInfo  ReqPacket_GetStoryDungeonSaveInfo -> ResPacket_GetStoryDungeonSaveInfo
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetStoryDungeonSaveInfo
{
    public long PacketId;
}

public class ResPacket_GetStoryDungeonSaveInfo
{
    public StoryDungeonSaveInfoFormat saveInfo;
    public long PacketId;
    public StoryDungeonSaveInfoFormat SaveInfo;
}

