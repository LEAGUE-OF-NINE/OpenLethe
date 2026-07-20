// /api/GetFriendsData  ReqPacket_GetFriends -> ResPacket_GetFriends
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetFriends
{
    public long PacketId;
}

public class ResPacket_GetFriends
{
    public List<UserPublicProfileFormat> friendprofileList;
    public List<UserPublicProfileFormat> sendprofileList;
    public List<UserPublicProfileFormat> receiveprofileList;
    public long PacketId;
}

