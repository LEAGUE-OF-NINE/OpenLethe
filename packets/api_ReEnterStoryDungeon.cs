// /api/ReEnterStoryDungeon  ReqPacket_ReEnterStoryDungeon -> ResPacket_ReEnterStoryDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ReEnterStoryDungeon
{
    public int stageid;
    public long PacketId;
}

public class ResPacket_ReEnterStoryDungeon
{
    public StoryDungeonSaveInfoFormat saveInfo;
    public List<int> nodesRecord;
    public uint isAllDie;
    public long PacketId;
    public StoryDungeonSaveInfoFormat SaveInfo;
    public List<int> NodesRecord;
    public uint IsAllDie;
}

