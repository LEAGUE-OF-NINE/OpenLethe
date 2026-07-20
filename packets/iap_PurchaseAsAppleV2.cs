// /iap/PurchaseAsAppleV2  ReqPacket_PurchaseAsAppleV2 -> ResPacket_PurchaseAsAppleV2
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseAsAppleV2
{
    public string productId;
    public string receipt;
    public string transactionId;
    public long PacketId;
}

public class ResPacket_PurchaseAsAppleV2
{
    public AppleIAP appleIAP;
    public long PacketId;
}

public class AppleIAP
{
    public string productID;
    public string transId;
    public IAP_PROCESS state;
}

public enum IAP_PROCESS { CANCELED, FINALIZED, REFUNDED, REPLACED, PENDING }

