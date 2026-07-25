// /api/ReEnterStoryMirrorDungeon  ReqPacket_ReEnterStoryMirrorDungeon -> ResPacket_ReEnterStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ReEnterStoryMirrorDungeon
{
    public int dungeonid;
    public long PacketId;
}

public class ResPacket_ReEnterStoryMirrorDungeon
{
    public StoryMirrorDungeonSaveInfoFormat saveInfo;
    public long PacketId;
    public StoryMirrorDungeonSaveInfoFormat SaveInfo;
}

