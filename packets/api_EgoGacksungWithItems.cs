// /api/EgoGacksungWithItems  ReqPacket_EgoGacksungWithItems -> ResPacket_EgoGacksungWithItems
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EgoGacksungWithItems
{
    public int egoId;
    public List<ItemFormat> usingPieces;
    public List<ItemFormat> usingPoints;
    public long PacketId;
}

public class ResPacket_EgoGacksungWithItems
{
    public long PacketId;
}

