// /api/AcceptReceivedFriendRequest  ReqPacket_AcceptReceivedFriendRequest -> ResPacket_AcceptReceivedFriendRequest
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcceptReceivedFriendRequest
{
    public string senderPublicUID;
    public long PacketId;
}

public class ResPacket_AcceptReceivedFriendRequest
{
    public uint success;
    public long PacketId;
}

