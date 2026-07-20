// /api/SendFriendRequest  ReqPacket_SendFriendRequest -> ResPacket_SendFriendRequest
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SendFriendRequest
{
    public string receiverPublicUID;
    public long PacketId;
}

public class ResPacket_SendFriendRequest
{
    public uint success;
    public UserPublicProfileFormat receiverprofile;
    public long PacketId;
}

