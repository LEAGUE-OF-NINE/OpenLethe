using OpenLethe.Resources;

namespace OpenLethe.Server;

/// The slice of static-data/railway-dungeon/*.json the server actually needs:
/// per-dungeon extra rewards and rotation buff-set rules. Every other field in
/// those files (uiConfig, sector, battle wiring, banner rewards) is client-side.
public sealed class RailwayCondition
{
    public string type = "";
    public long count;
}

public sealed class RailwayExtraReward
{
    public long id;
    public List<RailwayCondition> requiredConditions = new();
    public List<Wire.Element> rewards = new();
    public string? endDate;
}

public sealed class RailwayBuffSetOption
{
    public string type = "";
}

public sealed class RailwayBuffSetDef
{
    public long buffSetId;
    public List<long> idList = new();
    public List<RailwayBuffSetOption> selectOption = new();
}

/// How a dungeon's nodes group into clear-record entries. Dungeons 3, 4, 5, 6 and
/// 1001 declare one; dungeons 1, 2 and 1002 do not, and log one entry per node.
public sealed class RailwayNodeCollection
{
    public long collectionId;
    public List<long> nodeIds = new();
    public long formationTargetNode;
}

public sealed class RailwayDungeonDef
{
    public long id;
    // Rest-node recovery. hasRestHeal gates the HP heal on its own: dungeon 1001
    // declares restHPHealRate 100 with hasRestHeal false and heals nothing, while
    // still resetting MP - so isResetMPAtRestNode is NOT gated by it.
    public bool hasRestHeal;
    public long restHPHealRate;
    public long restMPHeal;
    public bool isResetMPAtRestNode;
    public List<RailwayExtraReward> extraRewards = new();
    public List<RailwayBuffSetDef> buffSet = new();
    public List<RailwayNodeCollection> nodeIdCollection_ForLog = new();
}

/// Reader for static-data/railway-dungeon. Nothing here infers one dungeon's
/// config from another's - a dungeon behaves exactly as its own file says.
///
/// One caveat on the bundled dump: it predates Refraction Railway 2's rerun, so
/// railway-dungeon-1002.json is ours, not upstream's. It declares only what the
/// capture in docs/flows(2) PROVES - line 2's buffSet, whose EXCLUDE_RECENT /
/// EXCLUDE_ACQUIRED_UNTIL_GET_ALL bookkeeping the capture reproduces byte for
/// byte - and an empty extraRewards, because the capture pins only the UNION of
/// that dungeon's rewards 6-10, never their per-id split. Replace the whole file
/// from a current dump when one is available; do not hand-fill extraRewards.
public static class RailwayData
{
    private static readonly Lazy<List<RailwayDungeonDef>> Defs =
        new(() => StaticData.GetList<RailwayDungeonDef>("static-data/railway-dungeon"));

    public static RailwayDungeonDef? Find(long dungeonId) =>
        Defs.Value.FirstOrDefault(d => d.id == dungeonId);

    /// Extra rewards for a dungeon; empty when static data declares none.
    public static List<RailwayExtraReward> ExtraRewards(long dungeonId) =>
        Find(dungeonId)?.extraRewards ?? new();

    /// Rotation buff sets for a dungeon; empty when static data declares none,
    /// which makes the pick bookkeeping default to EXCLUDE_RECENT.
    public static List<RailwayBuffSetDef> BuffSets(long dungeonId) =>
        Find(dungeonId)?.buffSet ?? new();

    /// Clear-record node groupings; empty means one log entry per node.
    public static List<RailwayNodeCollection> LogCollections(long dungeonId) =>
        Find(dungeonId)?.nodeIdCollection_ForLog ?? new();
}
