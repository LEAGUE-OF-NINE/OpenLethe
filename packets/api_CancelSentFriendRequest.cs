// /api/CancelSentFriendRequest  ReqPacket_CancelSentFriendRequest -> ResPacket_CancelSentFriendRequest
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_CancelSentFriendRequest
{
    public string receivedPublicUID;
    public long PacketId;
}

public class ResPacket_CancelSentFriendRequest
{
    public long PacketId;
}

