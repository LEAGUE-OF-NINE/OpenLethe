// /api/GetFriendSupportPersonalities  ReqPacket_GetFriendSupportPersonalities -> ResPacket_GetFriendSupportPersonalities
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetFriendSupportPersonalities
{
    public string publicUID;
    public long PacketId;
}

public class ResPacket_GetFriendSupportPersonalities
{
    public List<SupportPersonalitySlotFormat> supportpersonalities;
    public long PacketId;
}

