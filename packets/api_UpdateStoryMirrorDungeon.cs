// /api/UpdateStoryMirrorDungeon  ReqPacket_UpdateStoryMirrorDungeon -> ResPacket_UpdateStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateStoryMirrorDungeon
{
    public List<MirrorDungeonFormationFormat> formation;
    public long PacketId;
}

public class ResPacket_UpdateStoryMirrorDungeon
{
    public StoryMirrorDungeonSaveInfoFormat saveInfo;
    public long PacketId;
    public StoryMirrorDungeonSaveInfoFormat SaveInfo;
}

