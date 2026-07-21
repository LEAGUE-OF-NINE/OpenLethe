using OpenLethe.Resources;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Defaults;

/// Port of the subset of lethe-server/models/src/data/mod.rs that
/// load_user_data_all needs. Reads embedded static-data via OpenLethe.Resources.
public static class DefaultData
{
    public const string AcquireTime = "2023-09-27T01:00:00.000Z";
    public const long DefaultPersonalityLevel = 60;

    // Memoized builders: Lazy<T> defers the expensive scan/parse on first call.
    // Public methods return shallow copies to prevent mutation of the cache.
    // ponytail: the "shallow copy" only clones the List<T> container (new(_field.Value));
    // the element instances inside are shared across every caller and every call. A future
    // handler that mutates a returned element in place (e.g. a level-up bumping .level) would
    // corrupt the process-wide cache for all other accounts. Such a consumer must deep-copy the
    // element itself (or these builders must switch to per-call deep copies).
    private static readonly Lazy<List<Ego>> _egos = new(BuildFormattedEgos);
    private static readonly Lazy<List<ResultPersonality>> _personalities = new(BuildFormattedPersonalities);
    private static readonly Lazy<List<UserUnlockCode>> _userCodes = new(BuildFormattedUserCodes);
    private static readonly Lazy<List<long>> _danteIds = new(BuildDanteAbilityIds);
    private static readonly Lazy<List<MainChapterState>> _chapterState = new(BuildMainChapterState);

    // static-data/ego/ -> owned-ego defaults.
    public static List<Ego> GetFormattedEgos() => new(_egos.Value);

    private static List<Ego> BuildFormattedEgos() =>
        StaticData.GetList<IdStruct>("static-data/ego/")
            .Select(e => new Ego { ego_id = e.id, gacksung = 4, acquire_time = AcquireTime })
            .ToList();

    // static-data/personality/ -> personality defaults; order_id is the index.
    public static List<ResultPersonality> GetFormattedPersonalities() => new(_personalities.Value);

    private static List<ResultPersonality> BuildFormattedPersonalities() =>
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
    public static List<UserUnlockCode> GetFormattedUserCodes() => new(_userCodes.Value);

    private static List<UserUnlockCode> BuildFormattedUserCodes() =>
        StaticData.GetList<IdStruct>("static-data/unlockcode/")
            .Select(c => new UserUnlockCode { unlockcode = c.id, expireDate = "" })
            .ToList();

    // static-data/dante-ability/ -> ability id list.
    public static List<long> GetDanteAbilityIds() => new(_danteIds.Value);

    private static List<long> BuildDanteAbilityIds() =>
        StaticData.GetList<IdStruct>("static-data/dante-ability/")
            .Select(d => d.id)
            .ToList();

    // static-data/stagenodereward/ -> MainChapterState tree.
    // MainChapterState.id = nodeid / 10000; Subcss.id = nodeid / 100; Nss.id = nodeid.
    // ponytail: sort node ids then group (canonical, deterministic). Rust groups
    // consecutive over unsorted iteration order; identical for ordered data.
    public static List<MainChapterState> LoadMainChapterState() => new(_chapterState.Value);

    private static List<MainChapterState> BuildMainChapterState()
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
