// /api/FindFriend  ReqPacket_FindFriend -> ResPacket_FindFriend
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_FindFriend
{
    public string publicUID;
    public long PacketId;
}

public class ResPacket_FindFriend
{
    public bool success;
    public UserPublicProfileFormat friendprofile;
    public long PacketId;
}

