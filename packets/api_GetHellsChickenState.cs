// /api/GetHellsChickenState  ReqPacket_GetHellsChickenState -> ResPacket_GetHellsChickenState
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetHellsChickenState
{
    public long PacketId;
}

public class ResPacket_GetHellsChickenState
{
    public int dollsNum;
    public List<int> rewardState;
    public long PacketId;
}

