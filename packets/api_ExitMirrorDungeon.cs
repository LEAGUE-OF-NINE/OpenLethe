// /api/ExitMirrorDungeon  ReqPacket_ExitMirrorDungeon -> ResPacket_ExitMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExitMirrorDungeon
{
    public long PacketId;
}

public class ResPacket_ExitMirrorDungeon
{
    public int isEndDungeon;
    public int isclear;
    public List<DungeonStatisticsDataFormat> statistics;
    public long PacketId;
    public bool IsClear;
    public List<DungeonStatisticsDataFormat> Statistics;
}

