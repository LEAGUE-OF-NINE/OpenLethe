// /api/GetUserBanners  ReqPacket_GetUserBanners -> ResPacket_GetUserBanners
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetUserBanners
{
    public long PacketId;
}

public class ResPacket_GetUserBanners
{
    public List<UserBannerDataFormat> banners;
    public long PacketId;
}

