// /api/UpdateUserProfile  ReqPacket_UpdateUserProfile -> ResPacket_UpdateUserProfile
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateUserProfile
{
    public int illustId;
    public int illustGacksungLevel;
    public int sentenceId;
    public int wordId;
    public List<UserPublicBannerFormat> banners;
    public List<SupportPersonalitySlotFormat> supportPersonalities;
    public long PacketId;
}

public class ResPacket_UpdateUserProfile
{
    public long PacketId;
}

