using System.Text.Json.Serialization;
using OpenLethe.Resources;

namespace OpenLethe.Server.Wire;

/// What Account.BossRaidSaveInfo holds: the client-facing save plus the run state the
/// real server tracks beside it (the raid's durante pool and each finished party).
/// Older rows stored a bare BossRaidSaveDataFormat; BossRaidRules.Load still reads those.
public sealed class BossRaidStore
{
    public global::BossRaidSaveDataFormat? save;

    /// The durante pool for this run: danteAbilityCategory of the raid entry, its ability ids,
    /// how many uses are left and which abilities were spent (each has limit 1).
    public DanteAbility? dante;

    /// One entry per party that has fought (idx = the try it was used on).
    public List<BossRaidPartyDetail> parties = new();

    /// The party sent by the last StartBossRaidBattle, recorded on its ExitBossRaidBattle.
    public List<BossRaidPartyPersonality> pending = new();
}

public sealed class BossRaidPartyDetail
{
    public int idx;
    public int turn;
    public List<BossRaidPartyPersonality> personalities = new();
    public List<BossRaidStatistic> statistics = new();
}

public sealed class BossRaidPartyPersonality
{
    public int g;
    public int lv;
    public int pid;
    public int pord;
}

public sealed class BossRaidStatistic
{
    public int gd;
    public int id;
    public int rd;
}

/// Wire shape of the real server's EndBossRaid logData (see the captured flow): plain
/// strings for dates, unlike the client's BossRaidLogDataFormat with its DateUtil members.
public sealed class BossRaidLogData
{
    public int idx;
    public int raidId;
    public int difficulty;
    public int clearturn;
    public List<BossRaidPartyDetail> partydetails = new();
    public string? date;
    public string? startdate;
}

public sealed class GetBossRaidSaveInfoResult
{
    public global::BossRaidSaveDataFormat? saveInfo;
    public List<BossRaidPartyDetail> partyDatas = new();
    public BossRaidLogData? logData;
}

public sealed class EndBossRaidResult
{
    public global::BossRaidSaveDataFormat? saveInfo;
    public BossRaidLogData? logData;
    public List<Element> rewards = new();
}

// ---- static data -------------------------------------------------------------------

/// static-data/boss-raid entry (only the fields the server needs).
public sealed class BossRaidStatic
{
    public int id;
    public int maxPartyCount;
    public long danteAbilityCategory;
    public List<BossRaidDifficultyStatic> difficulties = new();
}

public sealed class BossRaidDifficultyStatic
{
    public string difficulty = "";
    public long stageId;
    public List<BossRaidRewardStatic> rewards = new();
}

public sealed class BossRaidRewardStatic
{
    public string type = "";
    public long id;
    public long num;
}

/// One static-data/dante-ability-common-data file (unlisted: the file IS the object).
public sealed class DanteAbilityCommonData
{
    public long defaultCount;
    public List<DanteAbilityCategoryStatic> categories = new();
    public List<DanteAbilityAdditionalCount> additionalCountInfos = new();
}

public sealed class DanteAbilityCategoryStatic
{
    public long categoryId;
    public List<long> availableAbilityIds = new();
    public List<long> availableAdditionalCountIds = new();
}

public sealed class DanteAbilityAdditionalCount
{
    public long id;
    public long additionalCount;
}

public static class BossRaidRules
{
    private static readonly Lazy<List<BossRaidStatic>> _raids =
        new(() => StaticData.GetList<BossRaidStatic>("static-data/boss-raid/").OrderBy(r => r.id).ToList());

    private static readonly Lazy<List<DanteAbilityCommonData>> _danteCommon =
        new(() => StaticData.GetUnlisted<DanteAbilityCommonData>("static-data/dante-ability-common-data/"));

    /// The real server's fallback when a raid's durante pool can't be derived (it sends 2).
    public const long DefaultDanteCount = 2;

    /// Fallback when a raid entry is missing; every shipped boss-raid uses 3.
    public const int DefaultMaxPartyCount = 3;

    public static IReadOnlyList<BossRaidStatic> Raids => _raids.Value;

    public static BossRaidStatic? Raid(int raidId) => _raids.Value.FirstOrDefault(r => r.id == raidId);

    public static int MaxPartyCount(int raidId) =>
        Raid(raidId)?.maxPartyCount is > 0 and var n ? n : DefaultMaxPartyCount;

    /// The distinct durante categories the boss raids use (3, 4, 6 in the shipped data).
    public static IEnumerable<long> DanteCategories() =>
        _raids.Value.Select(r => r.danteAbilityCategory).Distinct().OrderBy(c => c);

    /// A full, unused durante pool for `category`. Lethe accounts own every unlock code, so
    /// every additional-count bonus applies: defaultCount 0 + HOKMA 1 + CHESED 1 = 2, which is
    /// what the real server sent on EnterBossRaid.
    public static DanteAbility FreshDantePool(long category)
    {
        foreach (var common in _danteCommon.Value)
        {
            var cat = common.categories.FirstOrDefault(c => c.categoryId == category);
            if (cat is null) continue;
            var bonus = common.additionalCountInfos
                .Where(a => cat.availableAdditionalCountIds.Contains(a.id))
                .Sum(a => a.additionalCount);
            return new DanteAbility
            {
                category = category,
                abilityids = new List<long>(cat.availableAbilityIds),
                remaincount = common.defaultCount + bonus,
                usedlogs = new(),
            };
        }
        return new DanteAbility
        {
            category = category,
            abilityids = new List<long> { 90101, 80101, 70101, 60101 },
            remaincount = DefaultDanteCount,
            usedlogs = new(),
        };
    }

    /// The difficulty entry for the client's int difficulty (BOSS_RAID_DIFFICULTY: 1 NORMAL, 2 HARD).
    public static BossRaidDifficultyStatic? Difficulty(int raidId, int difficulty)
    {
        var name = difficulty switch { 1 => "NORMAL", 2 => "HARD", _ => null };
        return name is null ? null : Raid(raidId)?.difficulties.FirstOrDefault(d => d.difficulty == name);
    }

    public static BossRaidStore? Load(string? json)
    {
        var store = AccountFields.Get<BossRaidStore>(json);
        if (store?.save is not null) return store;

        // Legacy row: a bare BossRaidSaveDataFormat.
        var legacy = AccountFields.Get<global::BossRaidSaveDataFormat>(json);
        if (legacy is null || legacy.raidId == 0) return null;
        return new BossRaidStore
        {
            save = legacy,
            dante = FreshDantePool(Raid(legacy.raidId)?.danteAbilityCategory ?? 0),
        };
    }

    /// Apply ExitBossRaidBattle's danteAbilityUsageLogs: each use costs one from the pool and
    /// is logged, so the client greys that ability out for the rest of the run.
    public static void SpendDante(DanteAbility pool, IEnumerable<global::DanteAbilityUsedLogFormat>? usage)
    {
        if (usage is null) return;
        pool.usedlogs ??= new();
        foreach (var u in usage)
        {
            if (u is null || u.count <= 0) continue;
            pool.remaincount = Math.Max(0, pool.remaincount - u.count);
            var existing = pool.usedlogs.FirstOrDefault(l => l.abilityId == u.abilityId);
            if (existing is null) pool.usedlogs.Add(new DanteAbilityUsedLog { abilityId = u.abilityId, count = u.count });
            else existing.count += u.count;
        }
    }
}
