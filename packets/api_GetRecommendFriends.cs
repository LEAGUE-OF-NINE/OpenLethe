// /api/GetRecommendFriends  ReqPacket_GetRecommendFriends -> ResPacket_GetRecommendFriends
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetRecommendFriends
{
    public long PacketId;
}

public class ResPacket_GetRecommendFriends
{
    public List<UserPublicProfileFormat> recomendedFriends;
    public long PacketId;
}

