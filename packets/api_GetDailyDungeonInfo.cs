// /api/GetDailyDungeonInfo  ReqPacket_GetDailyDungeonInfo -> ResPacket_GetDailyDungeonInfo
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetDailyDungeonInfo
{
    public long PacketId;
}

public class ResPacket_GetDailyDungeonInfo
{
    public List<ExpDungeonClearInfoFormat> expDungeonClearInfo;
    public List<ThreadDungeonClearInfoFormat> threadDungeonClearInfo;
    public string date;
    public long PacketId;
    public List<ExpDungeonClearInfoFormat> ExpDungeonClearInfo;
    public string Date;
    public List<ThreadDungeonClearInfoFormat> ThreadDungeonClearInfo;
}

