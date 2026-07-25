// /api/UseEgoGacksungItem  ReqPacket_UseEgoGacksungItem -> ResPacket_UseEgoGacksungItem
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UseEgoGacksungItem
{
    public int targetEgoId;
    public ItemFormat usingItem;
    public long PacketId;
}

public class ResPacket_UseEgoGacksungItem
{
    public long PacketId;
}

