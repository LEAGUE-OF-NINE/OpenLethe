namespace OpenLethe.Data;

/// One player account. Identity columns are ours; every game-data column is an
/// opaque client-shaped JSON document that the server stores and returns
/// unchanged (deserialized by handlers with global::PacketJson.Options).
public sealed class Account
{
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public int IngameId { get; set; }
    /// Discord snowflake, set by the /auth OAuth flow. Null for accounts made
    /// through the username-only dev login, so it is nullable and only unique
    /// among the rows that have one.
    public string? DiscordId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Game-data documents. Defaults are neutral placeholders; cycle 3 populates
    // game-meaningful defaults when load_user_data_all and the resources project land.
    public string Formations { get; set; } = "{}";
    public string Egos { get; set; } = "[]";
    public string Personalities { get; set; } = "[]";
    public string Announcers { get; set; } = "[]";
    public string UserInfo { get; set; } = "{}";
    public string CustomTheme { get; set; } = "{}";
    public string CustomIdentities { get; set; } = "{}";
    public string CustomEgos { get; set; } = "{}";
    public string MdSaveInfo { get; set; } = "{}";
    public string StorySaveInfo { get; set; } = "{}";
    public string StoryMdSaveInfo { get; set; } = "{}";
    // dungeonId -> RailwayRun (save + node data + clear logs). One column, because
    // railway state is per dungeon and every handler reads and writes it as a unit.
    public string RailwaySaveInfo { get; set; } = "{}";
    public string ChapterState { get; set; } = "{}";
    public string BossRaidSaveInfo { get; set; } = "{}";
}
