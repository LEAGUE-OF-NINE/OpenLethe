// /api/ExitStoryDungeon  ReqPacket_ExitStoryDungeon -> ResPacket_ExitStoryDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitStoryDungeon
{
    public int mainchapterid;
    public int subchapterid;
    public int nodeid;
    public int stageid;
    public long PacketId;
}

public class ResPacket_ExitStoryDungeon
{
    public StoryDungeonSaveInfoFormat saveInfo;
    public bool iswin;
    public int cleartype;
    public int addexptouser;
    public List<StagePersonalityInfoFormat> personalityinfos;
    public List<Element> expticket;
    public List<Element> rewarditem;
    public List<Element> exrewarditem;
    public List<Element> firstrewarditem;
    public Element givebackstaminabyDefeat;
    public List<DungeonStatisticsDataFormat> statistics;
    public bool isGacksung;
    public long PacketId;
    public StoryDungeonSaveInfoFormat SaveInfo;
    public List<DungeonStatisticsDataFormat> Statistics;
    public bool IsGacksung;
}

