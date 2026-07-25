// /api/EnterStoryMirrorDungeon  ReqPacket_EnterStoryMirrorDungeon -> ResPacket_EnterStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterStoryMirrorDungeon
{
    public int dungeonid;
    public int idx;
    public long PacketId;
}

public class ResPacket_EnterStoryMirrorDungeon
{
    public StoryMirrorDungeonSaveInfoFormat saveInfo;
    public long PacketId;
    public StoryMirrorDungeonSaveInfoFormat SaveInfo;
}

