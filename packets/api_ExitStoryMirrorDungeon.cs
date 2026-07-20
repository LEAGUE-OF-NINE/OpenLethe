// /api/ExitStoryMirrorDungeon  ReqPacket_ExitStoryMirrorDungeon -> ResPacket_ExitStoryMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitStoryMirrorDungeon
{
    public long PacketId;
}

public class ResPacket_ExitStoryMirrorDungeon
{
    public StoryMirrorDungeonSaveInfoFormat saveInfo;
    public int isclear;
    public List<DungeonStatisticsDataFormat> statistics;
    public int cleartype;
    public int adduserexp;
    public List<StagePersonalityInfoFormat> personalityinfos;
    public List<Element> normalrewards;
    public List<Element> exrewards;
    public List<Element> firstrewarditem;
    public List<Element> expticket;
    public Element givebackstaminabyDefeat;
    public long PacketId;
    public bool IsClear;
    public List<DungeonStatisticsDataFormat> Statistics;
}

