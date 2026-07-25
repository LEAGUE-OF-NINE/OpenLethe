// /api/ReturnSavePointStoryDungeonMap  ReqPacket_ReturnSavePointStoryDungeonMap -> ResPacket_ReturnSavePointStoryDungeonMap
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ReturnSavePointStoryDungeonMap
{
    public long PacketId;
}

public class ResPacket_ReturnSavePointStoryDungeonMap
{
    public StoryDungeonCurrentInfoFormat currentInfo;
    public long PacketId;
    public StoryDungeonCurrentInfoFormat CurrentInfo;
}

