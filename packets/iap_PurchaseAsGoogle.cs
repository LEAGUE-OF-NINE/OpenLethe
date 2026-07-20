// /iap/PurchaseAsGoogle  ReqPacket_PurchaseAsGoogle -> ResPacket_PurchaseAsGoogle
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseAsGoogle
{
    public string productId;
    public string receipt;
    public long PacketId;
}

public class ResPacket_PurchaseAsGoogle
{
    public long PacketId;
}

