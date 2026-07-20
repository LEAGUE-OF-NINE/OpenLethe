// /api/LoadUserDataAll  ReqPacket_LoadUserDataAll -> ResPacket_LoadUserDataAll
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_LoadUserDataAll
{
    public long PacketId;
}

public class ResPacket_LoadUserDataAll
{
    public string secession_Date;
    public DateUtil _secessionDate;
    public UserPublicProfileWithSupportersFormat profile;
    public bool isExistReceiveFriendRequest;
    public int danteNoteTodayPage;
    public List<DailyLoginRewardStateFormat> dailyLoginRewardStates;
    public int dailyLoginWeekId;
    public int dailyLoginId;
    public int showedWeekByMinistory;
    public List<UserBannerDataFormat> userbanners;
    public List<UserProfileBorderFormat> leftBorders;
    public List<UserProfileBorderFormat> rightBorders;
    public List<UserProfileEgobackgroundFormat> egoBackgrounds;
    public string date;
    public long PacketId;
    public bool IsExistReceiveFriendRequest;
    public DateUtil SecessionDate;
}

public class UserPublicProfileWithSupportersFormat
{
    public List<SupportPersonalitySlotFormat> support_personalities;
    public string public_uid;
    public int illust_id;
    public int illust_gacksung_level;
    public int leftborder_id;
    public int rightborder_id;
    public int egobackground_id;
    public int sentence_id;
    public int word_id;
    public List<UserPublicBannerFormat> banners;
    public int level;
    public string date;
}

