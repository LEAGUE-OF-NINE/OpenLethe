// /api/ExchangeTwine  ReqPacket_ExchangeTwine -> ResPacket_ExchangeTwine
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ExchangeTwine
{
    public List<ItemFormat> paidPieces;
    public long PacketId;
}

public class ResPacket_ExchangeTwine
{
    public long PacketId;
}

