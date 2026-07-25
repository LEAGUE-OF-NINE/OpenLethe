// /iap/UpdateSteamPendingPurchase  ReqPacket_UpdateSteamPendingPurchase -> ResPacket_UpdateSteamPendingPurchase
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateSteamPendingPurchase
{
    public long PacketId;
}

public class ResPacket_UpdateSteamPendingPurchase
{
    public List<string> finalizedProductIds;
    public int pendingLogCount;
    public long PacketId;
}

