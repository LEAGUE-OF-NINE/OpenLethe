// /api/UseGachaPityPoint  ReqPacket_UseGachaPityPoint -> ResPacket_UseGachaPityPoint
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UseGachaPityPoint
{
    public int gachaId;
    public int targetIdx;
    public long PacketId;
}

public class ResPacket_UseGachaPityPoint
{
    public List<GachaLogDetail> gachaLogDetails;
    public long PacketId;
}

