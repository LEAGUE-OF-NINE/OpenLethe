// /iap/InitPurchase  ReqPacket_InitPurchase -> ResPacket_InitPurchase
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_InitPurchase
{
    public string productId;
    public long PacketId;
}

public class ResPacket_InitPurchase
{
    public string resultState;
    public long PacketId;
    public bool IsPurchasable;
}

