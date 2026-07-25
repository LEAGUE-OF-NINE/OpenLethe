// /api/RejectReceivedFriendRequest  ReqPacket_RejectReceivedFriendRequest -> ResPacket_RejectReceivedFriendRequest
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RejectReceivedFriendRequest
{
    public string senderPublicUID;
    public long PacketId;
}

public class ResPacket_RejectReceivedFriendRequest
{
    public long PacketId;
}

