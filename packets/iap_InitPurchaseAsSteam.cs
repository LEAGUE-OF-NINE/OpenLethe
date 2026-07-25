// /iap/InitPurchaseAsSteam  ReqPacket_InitPurchaseAsSteam -> ResPacket_InitPurchaseAsSteam
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_InitPurchaseAsSteam
{
    public string productId;
    public string steamId;
    public string language;
    public string productDesc;
    public long PacketId;
}

public class ResPacket_InitPurchaseAsSteam
{
    public long PacketId;
}

