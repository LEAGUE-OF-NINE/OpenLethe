using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace OpenLethe.Server.Wire;

// Server-authored port of lethe-server/models/src/types.rs UpdatedFormat and its
// members. Field names are the wire contract - do not rename. Public fields
// (PacketJson.Options has IncludeFields=true). Namespaced so member names like
// UserInfo do not collide with global packet types.

public sealed class UpdatedFormat
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? isInitialized;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public UserInfo? userInfo;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<ResultPersonality>? personalityList;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<Ego>? egoList;
    // formationList is a passthrough of the stored column - no Formation1 class.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<JsonNode>? formationList;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public LobbyCg? lobbyCG;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<AcquiredFromLostGlobalPieces>? itemList;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public BattlePass? battlePass;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<MainChapterState>? mainChapterStateList;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<InitializedMail>? mailList;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Announcer? announcer;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<Membership>? membershipList;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<UserUnlockCode>? userUnlockCodeList;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<EventRewardState>? eventRewardStateList;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<DanteAbility>? danteAbilityList;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<PersonalitySkin>? personalitySkinList;
}

public sealed class UserInfo
{
    public long uid;
    public long level;
    public long exp;
    public long stamina;
    public string last_stamina_recover = "";
    public string first_login_today = "";
}

public sealed class Ego
{
    public long ego_id;
    public long gacksung;
    public string acquire_time = "";
}

public sealed class ResultPersonality
{
    public long personality_id;
    public long level;
    public long exp;
    public long gacksung;
    public long order_id;
    public long gacksung_illust_type;
    public string acquire_time = "";
}

public sealed class MainChapterState
{
    public long id;
    public List<Subcss> subcss = new();
}

public sealed class Subcss
{
    public long id;
    public List<Nss> nss = new();
    public List<long> rss = new();
}

public sealed class Nss
{
    public long id;
    public long ct;
    public long cn;
    public long dn;
}

public sealed class DanteAbility
{
    public long category;
    public List<long> abilityids = new();
    public long remaincount;
}

public sealed class PersonalitySkin
{
    public long id;
    public string regdate = "";
}

public sealed class UserUnlockCode
{
    public long unlockcode;
    public string expireDate = "";
}

public sealed class InitializedMail
{
    public long mail_id;
    public string sent_date = "";
    public string expiry_date = "";
    public long content_id;
    public List<Element> attachments = new();
    public List<string> parameters = new();
}

public sealed class Element
{
    [JsonPropertyName("type")] public string type_ = "";
    public long id;
    public long num;
    public List<string> tags = new();
}

public sealed class AcquiredFromLostGlobalPieces
{
    public long item_id;
    public long num;
}

public sealed class BattlePass
{
    public bool is_limbus;
    public long level;
    public long exp;
    public long today_rand_value;
    public long ex_reward_level;
    public long limbus_apply_level;
    public List<long> rewards_state = new();
    // ponytail: missions_state is always empty in load_user_data_all; typed as an
    // empty list so BattlePassMissionState need not be ported until a handler sets it.
    public List<object> missions_state = new();
    public long special_product_state;
    public long ex_reward_limbus_level;
}

public sealed class Announcer
{
    public List<long> announcer_ids = new();
    public List<long> cur_announcer_ids = new();
}

public sealed class LobbyCg
{
    public long characterId;
    public List<LobbyCgDetail> lobbycgdetails = new();
    public bool isShowProfile;
}

public sealed class LobbyCgDetail
{
    public long id;
    public long g;
}

public sealed class Membership
{
    public long iap_id;
    public string expiry_date = "";
}

public sealed class EventRewardState
{
    public long eventID;
    public long rewardID;
    public long count;
}
