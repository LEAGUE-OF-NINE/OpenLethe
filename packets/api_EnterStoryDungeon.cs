// /api/EnterStoryDungeon  ReqPacket_EnterStoryDungeon -> ResPacket_EnterStoryDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterStoryDungeon
{
    public int mainchapterid;
    public int subchapterid;
    public int nodeid;
    public int stageid;
    public List<StoryDungeonSaveUnitInfoFormat> personalities;
    public long PacketId;
}

public class ResPacket_EnterStoryDungeon
{
    public StoryDungeonSaveInfoFormat saveInfo;
    public List<int> nodesRecord;
    public long PacketId;
    public StoryDungeonSaveInfoFormat SaveInfo;
    public List<int> NodesRecord;
}

