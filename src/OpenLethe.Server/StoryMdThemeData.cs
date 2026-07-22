using System;
using System.Collections.Generic;
using System.Linq;
using OpenLethe.Resources;

namespace OpenLethe.Server;

// ponytail: Rust's story_dungeon_id_into_mirror_dungeon_theme (models/src/data/mod.rs:87)
// reads two static-data folders - static-data/story-mirror-dungeon-generatenodepool and
// static-data/story-mirror-dungeon-egogift-droppool - to build each story-MD's node/ego-gift
// pools. NEITHER folder exists in this repo's resources tree, nor in the Rust reference's
// (both trees diffed: 162 directories, identical, folder absent from both). In the reference
// server every pool therefore comes back empty, causing two live defects: (1)
// story_map_gen/mod.rs generate_battle_node is a `loop` that retries until some pool yields a
// node - with all pools empty it spins forever, reachable from the registered route
// /api/AcquireStartEgoGiftsStoryMirrorDungeon; (2) exit_story_mirror_dungeon_map_node.rs calls
// gen_range(0..ego_gift_pool.len()) on the empty pool and panics. Project owner approved
// resolving both by deriving the pools at runtime from real in-dataset game data instead of
// reproducing the defects or writing synthesized story-mirror-dungeon-* JSON into the
// resources tree. Derived from: static-data/dungeon (id/dungeonType/dungeonLogicType),
// static-data/dungeonMap (via the existing StoryMap types - floors/sectors/nodes),
// static-data/acquirable-egogifts-in-event (eventId -> egoGiftList), and
// static-data/mirrordungeon-egogift-droppool (fallback pool, reusing DungeonEgoGiftDropPool
// from MdTheme.cs). If the real story-mirror-dungeon-* folders ever ship, replace this whole
// derivation with a direct static-data load. Pools are built into HashSets and de-duplicated,
// unlike Rust's Vec-based push - so a map node repeated in the source data does not raise its
// own draw weight here. That's a side effect of reading a different data source by design, not
// a fidelity defect.

public sealed class DungeonConfig
{
    public long id;
    public string dungeonType = "";
    public string dungeonLogicType = "";
}

public sealed class AcquirableEgoGiftsInEvent
{
    public long eventId;
    public List<long> egoGiftList = new();
}

// Pools are shared, cached instances (see StoryMdThemeData.Themes) - exposed read-only so
// no caller can mutate the one instance handed out for a dungeon id across the whole process.
public sealed class StoryMdTheme
{
    public long id;
    public IReadOnlyList<long> battlePool = new List<long>();
    public IReadOnlyList<long> hardBattlePool = new List<long>();
    public IReadOnlyList<long> abBattlePool = new List<long>();
    public IReadOnlyList<long> eventPool = new List<long>();
    public IReadOnlyList<long> bossPool = new List<long>();
    public IReadOnlyList<long> egoGiftPool = new List<long>();

    // Rust's get_combined_ego_gift_pool appends specific_ego_gift_pool to ego_gift_pool, but
    // the synthesized story theme never populates a specific pool - so this is just egoGiftPool.
    public IReadOnlyList<long> GetCombinedEgoGiftPool() => egoGiftPool;
}

public static class StoryMdThemeData
{
    // ponytail: memoize every scan/derivation - handlers call GetTheme per request, data is static.
    private static readonly Lazy<List<DungeonConfig>> Dungeons =
        new(() => StaticData.GetList<DungeonConfig>("static-data/dungeon"));

    private static readonly Lazy<IReadOnlySet<long>> DungeonIds = new(() =>
        Dungeons.Value
            .Where(d => d.dungeonType == "STORY_DUNGEON" && d.dungeonLogicType == "MIRROR_DUNGEON")
            .Select(d => d.id)
            .ToHashSet());

    private static readonly Lazy<List<AcquirableEgoGiftsInEvent>> EventGifts =
        new(() => StaticData.GetList<AcquirableEgoGiftsInEvent>("static-data/acquirable-egogifts-in-event"));

    private static readonly Lazy<List<long>> FallbackEgoGiftPool = new(() =>
    {
        var pools = MdThemeData.DropPools();
        if (pools.Count == 0) return new List<long>();
        var maxDungeonId = pools.Max(p => p.dungeonId);
        var pool = pools.First(p => p.dungeonId == maxDungeonId);
        var gifts = new HashSet<long>(pool.egoGifts);
        gifts.ExceptWith(pool.excludeEgoGifts);
        return gifts.ToList();
    });

    private static readonly Lazy<Dictionary<long, StoryMdTheme>> Themes =
        new(() => DungeonIds.Value.ToDictionary(id => id, BuildTheme));

    public static IReadOnlySet<long> StoryMdDungeonIds => DungeonIds.Value;

    public static StoryMdTheme GetTheme(long dungeonId) =>
        Themes.Value.TryGetValue(dungeonId, out var theme) ? theme : new StoryMdTheme { id = dungeonId };

    private static StoryMdTheme BuildTheme(long dungeonId)
    {
        var theme = new StoryMdTheme { id = dungeonId };
        var map = StoryMapData.GetStoryMapById(dungeonId);
        if (map is null) return theme;

        var battle = new HashSet<long>();
        var hardBattle = new HashSet<long>();
        var abBattle = new HashSet<long>();
        var eventIds = new HashSet<long>();
        var boss = new HashSet<long>();

        foreach (var node in map.floors.SelectMany(f => f.sectors).SelectMany(s => s.nodes))
        {
            if (node.encounterId == 0) continue;
            var pool = node.encounter.ToUpperInvariant() switch
            {
                "BATTLE" => battle,
                "HARD_BATTLE" => hardBattle,
                "AB_BATTLE" => abBattle,
                "EVENT" => eventIds,
                "BOSS" => boss,
                _ => null, // START, SAVE, HARD_AB_BATTLE, etc. - ignored, matching Rust's match arms
            };
            pool?.Add(node.encounterId);
        }

        theme.battlePool = battle.ToList();
        theme.hardBattlePool = hardBattle.ToList();
        theme.abBattlePool = abBattle.ToList();
        theme.eventPool = eventIds.ToList();
        theme.bossPool = boss.ToList();

        var gifts = new HashSet<long>();
        foreach (var entry in EventGifts.Value)
            if (eventIds.Contains(entry.eventId))
                gifts.UnionWith(entry.egoGiftList);

        theme.egoGiftPool = gifts.Count > 0 ? gifts.ToList() : FallbackEgoGiftPool.Value;

        return theme;
    }
}
