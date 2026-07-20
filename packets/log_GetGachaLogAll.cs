// /log/GetGachaLogAll  ReqPacket_GetGachaLogAll -> ResPacket_GetGachaLogAll
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetGachaLogAll
{
    public long PacketId;
}

public class ResPacket_GetGachaLogAll
{
    public List<GachaLog> gachaLogs;
    public long PacketId;
}

public class GachaLog
{
    public int gachaId;
    public string gachaDate;
    public DateUtil _gachaDate;
    public int paymentId;
    public List<ItemFormat> payments;
    public List<GachaLogDetail> gachaLogDetails;
    public int GachaId;
    public DateUtil GachaDate;
    public int PaymentId;
    public List<ItemFormat> Payments;
    public List<GachaLogDetail> GachaLogDetails;
}

