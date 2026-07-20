// /iap/FinalizePurchaseAsSteam  ReqPacket_FinalizePurchaseAsSteam -> ResPacket_FinalizePurchaseAsSteam
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_FinalizePurchaseAsSteam
{
    public string orderId;
    public long PacketId;
}

public class ResPacket_FinalizePurchaseAsSteam
{
    public long PacketId;
}

