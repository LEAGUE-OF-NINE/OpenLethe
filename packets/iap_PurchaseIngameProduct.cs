// /iap/PurchaseIngameProduct  ReqPacket_PurchaseIngameProduct -> ResPacket_PurchaseIngameProduct
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseIngameProduct
{
    public int igProductId;
    public long PacketId;
}

public class ResPacket_PurchaseIngameProduct
{
    public long PacketId;
}

