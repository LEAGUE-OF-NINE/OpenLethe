// /api/PlayGacha  ReqPacket_PlayGacha -> ResPacket_PlayGacha
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PlayGacha
{
    public int gachaId;
    public int paymentId;
    public long PacketId;
}

public class ResPacket_PlayGacha
{
    public List<GachaLogDetail> gachaLogDetails;
    public long PacketId;
}

