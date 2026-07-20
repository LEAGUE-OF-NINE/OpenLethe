// /api/CheckSeasonLog  ReqPacket_CheckSeasonLog -> ResPacket_CheckSeasonLog
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_CheckSeasonLog
{
    public long PacketId;
}

public class ResPacket_CheckSeasonLog
{
    public SeasonLogFormat seasonLog;
    public long PacketId;
}

public class SeasonLogFormat
{
    public int seasonTo;
    public int seasonFrom;
    public List<Element> unreceivedBattlePassRewards;
    public List<ItemFormat> lostPieces;
    public List<ItemFormat> acquiredFromLostPieces;
    public List<ItemFormat> lostPackages;
    public List<ItemFormat> acquiredFromLostPackages;
    public List<ItemFormat> lostGlobalPieces;
    public List<ItemFormat> acquiredFromLostGlobalPieces;
    public string date;
}

