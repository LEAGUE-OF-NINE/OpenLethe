// /api/DeleteFriend  ReqPacket_DeleteFriend -> ResPacket_DeleteFriend
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_DeleteFriend
{
    public string deletedPublicUID;
    public long PacketId;
}

public class ResPacket_DeleteFriend
{
    public long PacketId;
}

