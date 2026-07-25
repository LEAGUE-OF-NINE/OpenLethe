// /api/PurchaseBattlePassLevel  ReqPacket_PurchaseBattlePassLevel -> ResPacket_PurchaseBattlePassLevel
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseBattlePassLevel
{
    public int level;
    public long PacketId;
}

public class ResPacket_PurchaseBattlePassLevel
{
    public long PacketId;
}

