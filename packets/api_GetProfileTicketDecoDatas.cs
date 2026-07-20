// /api/GetProfileTicketDecoDatas  ReqPacket_GetProfileTicketDecoDatas -> ResPacket_GetProfileTicketDecoDatas
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetProfileTicketDecoDatas
{
    public long PacketId;
}

public class ResPacket_GetProfileTicketDecoDatas
{
    public List<UserProfileBorderFormat> leftBorders;
    public List<UserProfileBorderFormat> rightBorders;
    public List<UserProfileEgobackgroundFormat> egoBackgrounds;
    public long PacketId;
}

