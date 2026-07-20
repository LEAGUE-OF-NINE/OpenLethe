// /api/GetDungeonSaveInfoAll  ReqPacket_GetDungeonSaveInfoAll -> ResPacket_GetDungeonSaveInfoAll
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetDungeonSaveInfoAll
{
    public long PacketId;
}

public class ResPacket_GetDungeonSaveInfoAll
{
    public StoryDungeonSaveInfoFormat storySaveInfo;
    public MirrorDungeonSaveInfoFormat mirrorOriginSaveInfo;
    public StoryMirrorDungeonSaveInfoFormat storyMirrorSaveInfo;
    public List<MirrorDungeonClearInfoFormat> mirrorDungeonClearInfos;
    public List<MirrorDungeonHistoryFormat> mirrorDungeonHistories;
    public long PacketId;
}

public class MirrorDungeonClearInfoFormat
{
    public int dungeonid;
    public int idx;
    public int clearnumber;
    public int defeatnumber;
}

