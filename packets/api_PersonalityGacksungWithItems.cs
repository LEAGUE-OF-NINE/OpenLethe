// /api/PersonalityGacksungWithItems  ReqPacket_PersonalityGacksungWithItems -> ResPacket_PersonalityGacksungWithItems
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PersonalityGacksungWithItems
{
    public int personalityId;
    public List<ItemFormat> usingPieces;
    public long PacketId;
}

public class ResPacket_PersonalityGacksungWithItems
{
    public long PacketId;
}

