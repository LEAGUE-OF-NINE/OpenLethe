namespace OpenLethe.Server.MirrorDungeon.Data;

// The account's custom MD content for the request being served: the packs, E.G.O gifts, gift drop
// pools and battle stages the client mod uploads from custom_limbus_data (POST
// /custom/upload/mirrordungeon-theme-floor, ego-gift-mirrordungeon, mirrordungeon-egogift-droppool
// and battle-mirrordungeon). The client owns everything else about
// a custom gift - its passives, locale, icon and script; the server only needs to know it exists,
// what it costs, its tier and keyword, and which pools hand it out.
//
// MdThemeData, MdEgoData and MdAbRewards lay the current catalog over their static tables, so every
// rule that looks up a pack, a gift, the season's drop pool or a stage's rewardList sees custom
// content without being handed it.
// HandlerContext.ResolveAsync sets it for any request that resolves the Md or StoryMd save; outside
// a request (unit tests, startup) there is none and the static tables answer alone.
//
// Uploaded ids win over static ones, as they always have for packs. A custom gift drops wherever a
// drop pool or a pack's gift pool lists it, and is kept off the shelves by globalExcludeEgoGifts,
// exactly like a built-in one.
public sealed class MdCatalog
{
    private readonly Dictionary<long, ThemeStatic> themes = new();
    private readonly Dictionary<long, MdEgo> gifts = new();
    private readonly List<DungeonEgoGiftDropPool> dropPools;
    private readonly Dictionary<long, AbStage> battles = new();

    private readonly Lazy<List<ThemeStatic>> allThemes;
    private readonly Lazy<List<MdEgo>> allGifts;
    private readonly Lazy<List<DungeonEgoGiftDropPool>> allDropPools;

    public MdCatalog(IEnumerable<ThemeStatic>? themes, IEnumerable<MdEgo>? gifts,
        IEnumerable<DungeonEgoGiftDropPool>? dropPools, IEnumerable<AbStage>? battles = null)
    {
        // Last upload of an id wins, as MdThemePool's overlay always did.
        foreach (var t in themes ?? Enumerable.Empty<ThemeStatic>())
            if (t is not null && t.id != 0) this.themes[t.id] = t;
        foreach (var g in gifts ?? Enumerable.Empty<MdEgo>())
            if (g is not null && g.id != 0) this.gifts[g.id] = g;
        this.dropPools = (dropPools ?? Enumerable.Empty<DungeonEgoGiftDropPool>()).Where(p => p is not null).ToList();
        foreach (var b in battles ?? Enumerable.Empty<AbStage>())
            if (b is not null && b.id != 0) this.battles[b.id] = b;

        allThemes = new(() => Overlay(MdThemeData.StaticThemes(), this.themes, t => t.id));
        allGifts = new(() => Overlay(MdEgoData.StaticGifts(), this.gifts, g => g.id));
        allDropPools = new(() => MergeDropPools(MdThemeData.StaticDropPools(), this.dropPools));
    }

    public bool IsEmpty => themes.Count == 0 && gifts.Count == 0 && dropPools.Count == 0 && battles.Count == 0;

    // The uploaded pack / gift with this id, null when the account has none (the static table answers).
    public ThemeStatic? Theme(long id) => themes.GetValueOrDefault(id);
    public MdEgo? Gift(long id) => gifts.GetValueOrDefault(id);
    public AbStage? Battle(long id) => battles.GetValueOrDefault(id);

    // The static tables with the uploads laid over them.
    public List<ThemeStatic> AllThemes => allThemes.Value;
    public List<MdEgo> AllGifts => allGifts.Value;
    public List<DungeonEgoGiftDropPool> AllDropPools => allDropPools.Value;

    // Static rows keep their order, an uploaded row replacing the one with its id in place; uploads
    // with new ids follow.
    private static List<T> Overlay<T>(List<T> statics, Dictionary<long, T> uploaded, Func<T, long> id)
    {
        var result = statics.Select(s => uploaded.TryGetValue(id(s), out var u) ? u : s).ToList();
        var seen = statics.Select(id).ToHashSet();
        result.AddRange(uploaded.Values.Where(u => !seen.Contains(id(u))));
        return result;
    }

    // An uploaded pool ADDS to the static pool with its dungeonId - its egoGifts drop, its
    // excludeEgoGifts / globalExcludeEgoGifts are withheld - so a mod lists only its own gifts and
    // survives a season update. A dungeonId the static tables lack becomes a pool of its own. The
    // static pools are copied, never touched.
    private static List<DungeonEgoGiftDropPool> MergeDropPools(List<DungeonEgoGiftDropPool> statics,
        List<DungeonEgoGiftDropPool> uploaded)
    {
        var result = statics.Select(Copy).ToList();
        foreach (var u in uploaded)
        {
            var target = result.FirstOrDefault(p => p.dungeonId == u.dungeonId);
            if (target is null) { result.Add(Copy(u)); continue; }
            Union(target.egoGifts, u.egoGifts);
            Union(target.excludeEgoGifts, u.excludeEgoGifts);
            Union(target.excludeTargetIdxList, u.excludeTargetIdxList);
            Union(target.globalExcludeEgoGifts, u.globalExcludeEgoGifts);
        }
        return result;
    }

    private static DungeonEgoGiftDropPool Copy(DungeonEgoGiftDropPool p) => new()
    {
        dungeonId = p.dungeonId,
        egoGifts = new(p.egoGifts),
        excludeEgoGifts = new(p.excludeEgoGifts),
        excludeTargetIdxList = new(p.excludeTargetIdxList),
        globalExcludeEgoGifts = new(p.globalExcludeEgoGifts),
    };

    private static void Union(List<long> into, List<long>? add)
    {
        foreach (var id in add ?? new List<long>())
            if (!into.Contains(id)) into.Add(id);
    }

    // ---- the current request's catalog ----

    // A mutable slot rather than the catalog itself: the AsyncLocal is set by the per-request
    // middleware (BeginRequest), and ResolveAsync fills the slot from inside an async method, whose
    // own AsyncLocal writes would not flow back out to the handler.
    private sealed class Slot { public MdCatalog? Catalog; }

    private static readonly AsyncLocal<Slot?> CurrentSlot = new();

    public static MdCatalog? Current => CurrentSlot.Value?.Catalog;

    // Opens an empty slot for the request; Program registers it ahead of the endpoints.
    public static void BeginRequest() => CurrentSlot.Value = new Slot();

    // Makes `catalog` the current request's (null, or an empty one, clears it).
    public static void SetCurrent(MdCatalog? catalog)
    {
        if (catalog is { IsEmpty: true }) catalog = null;
        if (CurrentSlot.Value is { } slot) slot.Catalog = catalog;
        else CurrentSlot.Value = new Slot { Catalog = catalog };
    }

    // Reads the account's upload columns (any of them may be the "[]"/"{}" placeholder).
    public static MdCatalog FromColumns(string? customTheme, string? customEgoGifts, string? customDropPools,
        string? customBattles = null) =>
        new(AccountFields.Get<List<ThemeStatic>>(customTheme),
            AccountFields.Get<List<MdEgo>>(customEgoGifts),
            AccountFields.Get<List<DungeonEgoGiftDropPool>>(customDropPools),
            AccountFields.Get<List<AbStage>>(customBattles));

    // Makes `catalog` current until disposed, for code that runs outside a request.
    public static IDisposable Use(MdCatalog? catalog)
    {
        var previous = CurrentSlot.Value;
        CurrentSlot.Value = new Slot { Catalog = catalog is { IsEmpty: true } ? null : catalog };
        return new Restore(previous);
    }

    private sealed class Restore(Slot? previous) : IDisposable
    {
        public void Dispose() => CurrentSlot.Value = previous;
    }
}
