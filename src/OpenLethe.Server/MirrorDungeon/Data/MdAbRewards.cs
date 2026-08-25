using OpenLethe.Resources;

namespace OpenLethe.Server.MirrorDungeon.Data;

// Boss/abno battle-stage rewardList lookup, keyed by stage id. Scans the whole
// battle-mirrordungeon folder: the e==6 floor bosses span 4 files (abbattle5-0, abbattle6-0,
// battle5-0, battle6-0) and the e==14 abno stages another; a single-file load only reached
// abbattle5-0. Verified against the md-extreme capture: no id appears in two files with a
// differing rewardList, so the folder-wide flatten is collision-free for both callers.
public static class MdAbRewards
{
    private static readonly Lazy<List<AbStage>> Stages =
        new(() => StaticData.GetList<AbStage>("static-data/battle-mirrordungeon"));

    public static List<AbReward>? GetByNodeId(long encounterId) =>
        Stages.Value.FirstOrDefault(s => s.id == encounterId)?.rewardList;
}
