// /iap/PurchaseAsApple  ReqPacket_PurchaseAsApple -> ResPacket_PurchaseAsApple
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseAsApple
{
    public string productId;
    public string receipt;
    public string transactionId;
    public long PacketId;
}

public class ResPacket_PurchaseAsApple
{
    public long PacketId;
}

