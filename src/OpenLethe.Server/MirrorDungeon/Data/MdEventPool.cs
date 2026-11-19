namespace OpenLethe.Server.MirrorDungeon.Data;

// Which events a floor's event nodes draw, beyond the pack's own pools (see MdEventPicker):
//   * floor-specific events - an event only a handful of generated (MD4) packs list is that
//     floor's own event, and retail makes it far likelier there than the shared ones;
//   * once-per-run events - an event that hands out a one-of-a-kind gift (a level-up 993xxx
//     gift such as Clockwork Spring's, Wealth, a cursed gift - anything the season's drop pool
//     lists in globalExcludeEgoGifts) is never generated again once the run has played it or
//     owns what it gives.
public static class MdEventPool
{
    // An event listed by at most this share of the MD4 packs' event pools counts as floor-specific.
    // In the shipped tables every shared event sits in 11+ of the 82 MD4 packs, the floor-specific
    // ones (901015, 901062, 901100-901104, 971056, 971088) in 1-7.
    private const double FloorSpecificShare = 0.1;

    private static readonly Lazy<HashSet<long>> FloorSpecific = new(() =>
    {
        var md4 = MdThemeData.StaticThemes().Where(t => t.IsMd4Floor).ToList();
        var counts = md4.SelectMany(t => t.mapGenOption.eventPool.Distinct())
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());
        return counts.Where(kv => kv.Value <= md4.Count * FloorSpecificShare).Select(kv => kv.Key).ToHashSet();
    });

    public static bool IsFloorSpecific(long eventId) => FloorSpecific.Value.Contains(eventId);

    // Level-up gifts (993001-993005) and the season's globally excluded gifts.
    private static readonly Lazy<HashSet<long>> UniqueGifts = new(() =>
        MdThemeData.StaticDropPools().SelectMany(p => p.globalExcludeEgoGifts).ToHashSet());

    public static bool IsUniqueGift(long giftId) => giftId is >= 993000 and < 994000 || UniqueGifts.Value.Contains(giftId);

    private static readonly Dictionary<long, HashSet<long>> OnceGiftsCache = new();

    // The one-of-a-kind gifts an event's whole chain (every option, every follow-up event) can
    // hand out. Empty for an ordinary event.
    public static HashSet<long> OncePerRunGifts(long eventId)
    {
        lock (OnceGiftsCache)
        {
            if (OnceGiftsCache.TryGetValue(eventId, out var cached)) return cached;
            var gifts = ChainGifts(eventId).Where(IsUniqueGift).ToHashSet();
            OnceGiftsCache[eventId] = gifts;
            return gifts;
        }
    }

    public static bool IsOncePerRun(long eventId) => OncePerRunGifts(eventId).Count > 0;

    // The once-per-run events among `candidates` this run may not see again: played already
    // (playedEventIds, the save's peids) or owning one of the gifts they give.
    public static HashSet<long> Spent(IEnumerable<long> candidates, IEnumerable<long> playedEventIds,
        IEnumerable<long> ownedGiftIds)
    {
        var played = playedEventIds.ToHashSet();
        var owned = ownedGiftIds.ToHashSet();
        return candidates.Where(id => IsOncePerRun(id)
                && (played.Contains(id) || OncePerRunGifts(id).Overlaps(owned)))
            .ToHashSet();
    }

    private static IEnumerable<long> ChainGifts(long rootId)
    {
        var seen = new HashSet<long>();
        var pending = new Stack<long>();
        pending.Push(rootId);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!seen.Add(id) || OpenLethe.Server.MdEventData.GetById(id) is not { } ev) continue;

            var forms = new List<OpenLethe.Server.ResultForm>();
            foreach (var option in ev.actionEvent?.eachOptionList ?? new())
                foreach (var result in option.resultList ?? new())
                {
                    if (result.nextEventID is { } next) pending.Push(next);
                    forms.AddRange((result.eventResultDataList ?? new()).Select(d => d.resultForm));
                }
            foreach (var er in ev.personalityEvent?.eventResults ?? new())
                foreach (var data in er.eventResultDataList ?? new())
                {
                    pending.Push(data.nextEventID);
                    forms.AddRange((data.eventResultDataList ?? new()).Select(w => w.resultForm));
                }

            foreach (var form in forms)
                if (form?.itemReward?.rewardId is { } gift && form.resultEffect.StartsWith("Get") && form.resultEffect.Contains("Egogift"))
                    yield return gift;
        }
    }
}

// Draws the ids of one floor's event nodes. Every node draws on its own, never one the floor
// already has, never a spent once-per-run event:
//   * a pack with a specialEventPool takes from it with specialEventProb (always, when the pack
//     names no probability - the fixed abno floors), else from its eventPool;
//   * on a generated (MD4) floor without one, the floor-specific events in the eventPool weigh
//     FloorSpecificWeight times as much as the shared ones.
// Pick returns 0 when the pack has nothing left; PickFallback then draws from every event the
// dungeon uses.
public sealed class MdEventPicker
{
    public const double FloorSpecificWeight = 20;

    private readonly ThemeStatic theme;
    private readonly HashSet<long> used = new();
    private readonly HashSet<long> blocked;

    public MdEventPicker(ThemeStatic theme, IEnumerable<long>? blocked = null)
    {
        this.theme = theme;
        this.blocked = blocked?.ToHashSet() ?? new HashSet<long>();
    }

    public void MarkUsed(long eventId) => used.Add(eventId);

    private bool Open(long id) => id != 0 && !used.Contains(id) && !blocked.Contains(id);

    public double WeightOf(long eventId) =>
        theme.IsMd4Floor && theme.mapGenOption.specialEventPool is not { Count: > 0 } && MdEventPool.IsFloorSpecific(eventId)
            ? FloorSpecificWeight
            : 1;

    public long Pick()
    {
        var opt = theme.mapGenOption;
        var special = (opt.specialEventPool ?? new List<long>()).Where(Open).Distinct().ToList();
        var regular = opt.eventPool.Where(Open).Distinct().ToList();

        long id = 0;
        if (special.Count > 0 && (opt.specialEventProb is not { } prob || Random.Shared.NextDouble() < prob))
            id = special[Random.Shared.Next(special.Count)];
        else if (regular.Count > 0)
            id = Weighted(regular);
        else if (special.Count > 0)
            id = special[Random.Shared.Next(special.Count)];

        if (id != 0) used.Add(id);
        return id;
    }

    // Any event of the dungeon this floor does not have yet - one not spent if possible. 0 only
    // when the dungeon has no events at all.
    public long PickFallback(IReadOnlyList<long> dungeonEvents)
    {
        var pool = dungeonEvents.Where(Open).ToList();
        if (pool.Count == 0) pool = dungeonEvents.Where(id => !used.Contains(id)).ToList();
        if (pool.Count == 0) pool = dungeonEvents.ToList();
        if (pool.Count == 0) return 0;
        var id = pool[Random.Shared.Next(pool.Count)];
        used.Add(id);
        return id;
    }

    private long Weighted(List<long> pool)
    {
        var weights = pool.Select(WeightOf).ToArray();
        var roll = Random.Shared.NextDouble() * weights.Sum();
        for (var i = 0; i < pool.Count; i++)
        {
            if (roll < weights[i]) return pool[i];
            roll -= weights[i];
        }
        return pool[^1];
    }
}
