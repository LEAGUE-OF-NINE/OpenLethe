// /api/GetRailwayDungeonExtraRewardStates  ReqPacket_GetRailwayDungeonExtraRewardStates -> ResPacket_GetRailwayDungeonExtraRewardStates
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetRailwayDungeonExtraRewardStates
{
    public List<int> dungeonIds;
    public long PacketId;
}

public class ResPacket_GetRailwayDungeonExtraRewardStates
{
    public List<RailwayExtraRewardStateByDungeonIdFormat> list;
    public long PacketId;
}

public class RailwayExtraRewardStateByDungeonIdFormat
{
    public int dungeonId;
    public List<RailwayExtraRewardStateFormat> extraRewardState;
}

