// /api/SaveMiniStoryWeek  ReqPacket_SaveMiniStoryWeek -> ResPacket_SaveMiniStoryWeek
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SaveMiniStoryWeek
{
    public int weekId;
    public long PacketId;
}

public class ResPacket_SaveMiniStoryWeek
{
    public long PacketId;
}

