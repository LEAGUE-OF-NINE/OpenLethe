// /api/SetPersonalityGacksungIllust  ReqPacket_SetPersonalityGacksungIllust -> ResPacket_SetPersonalityGacksungIllust
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SetPersonalityGacksungIllust
{
    public int personalityId;
    public int type;
    public long PacketId;
}

public class ResPacket_SetPersonalityGacksungIllust
{
    public long PacketId;
}

