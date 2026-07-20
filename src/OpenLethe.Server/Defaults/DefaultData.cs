using OpenLethe.Resources;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Defaults;

/// Port of the subset of lethe-server/models/src/data/mod.rs that
/// load_user_data_all needs. Reads embedded static-data via OpenLethe.Resources.
public static class DefaultData
{
    public const string AcquireTime = "2023-09-27T01:00:00.000Z";
    public const long DefaultPersonalityLevel = 60;

    // static-data/ego/ -> owned-ego defaults.
    public static List<Ego> GetFormattedEgos() =>
        StaticData.GetList<IdStruct>("static-data/ego/")
            .Select(e => new Ego { ego_id = e.id, gacksung = 4, acquire_time = AcquireTime })
            .ToList();

    // static-data/personality/ -> personality defaults; order_id is the index.
    public static List<ResultPersonality> GetFormattedPersonalities() =>
        StaticData.GetList<IdStruct>("static-data/personality/")
            .Select((p, i) => new ResultPersonality
            {
                personality_id = p.id,
                order_id = i,
                gacksung_illust_type = 1,
                gacksung = 4,
                acquire_time = AcquireTime,
                level = DefaultPersonalityLevel,
                exp = 100,
            })
            .ToList();

    // static-data/unlockcode/ -> user unlock codes.
    public static List<UserUnlockCode> GetFormattedUserCodes() =>
        StaticData.GetList<IdStruct>("static-data/unlockcode/")
            .Select(c => new UserUnlockCode { unlockcode = c.id, expireDate = "" })
            .ToList();

    // static-data/dante-ability/ -> ability id list.
    public static List<long> GetDanteAbilityIds() =>
        StaticData.GetList<IdStruct>("static-data/dante-ability/")
            .Select(d => d.id)
            .ToList();

    // static-data/stagenodereward/ -> MainChapterState tree.
    // MainChapterState.id = nodeid / 10000; Subcss.id = nodeid / 100; Nss.id = nodeid.
    // ponytail: sort node ids then group (canonical, deterministic). Rust groups
    // consecutive over unsorted iteration order; identical for ordered data.
    public static List<MainChapterState> LoadMainChapterState()
    {
        var nodeIds = StaticData.GetList<NodeIdStruct>("static-data/stagenodereward/")
            .Select(n => n.nodeid)
            .OrderBy(id => id)
            .ToList();

        return nodeIds
            .GroupBy(id => id / 10000)
            .Select(chapter => new MainChapterState
            {
                id = chapter.Key,
                subcss = chapter
                    .GroupBy(id => id / 100)
                    .Select(sub => new Subcss
                    {
                        id = sub.Key,
                        rss = new List<long> { 1, 2, 3, 10 },
                        nss = sub.Select(nodeId => new Nss { id = nodeId, ct = 2, cn = 1, dn = 0 }).ToList(),
                    })
                    .ToList(),
            })
            .ToList();
    }
}
