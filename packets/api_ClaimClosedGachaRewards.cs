// /api/ClaimClosedGachaRewards  ReqPacket_ClaimClosedGachaRewards -> ResPacket_ClaimClosedGachaRewards
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ClaimClosedGachaRewards
{
    public long PacketId;
}

public class ResPacket_ClaimClosedGachaRewards
{
    public List<PityPoint> pityPointDataList;
    public long PacketId;
}

public class PityPoint
{
    public int gachaID;
    public int pityNumber;
}

