using System;
using System.Collections.Generic;
using System.Linq;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server;

// Port of lethe-server server/src/api/md/map/map_gen.rs. RNG uses Random.Shared (non-
// deterministic like Rust's thread_rng) - callers/tests must assert structural invariants,
// never exact generated ids. See memory: mirror-dungeon-rng-is-nondeterministic.

public struct DungeonNode
{
    public long node_type;
    public long encounter_id;
}

public readonly record struct DungeonCoordinate(int x, int y);

public sealed class DungeonGraph
{
    public const int Width = 16;
    public const int Height = 3;

    public readonly DungeonNode?[,] nodes = new DungeonNode?[Width, Height];
    public readonly HashSet<(DungeonCoordinate from, DungeonCoordinate to)> edges = new();
    public readonly HashSet<DungeonCoordinate> startingNodes = new();

    // Port of DungeonGraph::add_edge - bounds + node-existence checks exactly as Rust
    // (Rust's `as usize` cast on a negative i32 wraps to a huge value, which fails the
    // `>= W`/`>= H` bound check; we get the same "false" result via an explicit guard).
    public bool AddEdge(DungeonCoordinate from, DungeonCoordinate to)
    {
        if (from.x < 0 || from.y < 0 || to.x < 0 || to.y < 0) return false;
        if (from.x >= Width || from.y >= Height) return false;
        if (to.x >= Width || to.y >= Height) return false;

        if (nodes[from.x, from.y] is null) return false;
        if (nodes[to.x, to.y] is null) return false;

        edges.Add((from, to));
        return true;
    }

    public List<Ns> EncodeAsMd(long floor)
    {
        var dict = new Dictionary<long, Ns>();

        for (var w = 0; w < Width; w++)
        {
            for (var h = 0; h < Height; h++)
            {
                var node = nodes[w, h];
                if (node is null) continue;
                var nid = MdMapGen.NodeId(floor, w + 1, h);
                dict[nid] = new Ns
                {
                    f = floor,
                    s = w + 1,
                    nid = nid,
                    e = node.Value.node_type,
                    eid = node.Value.encounter_id,
                    nnids = new List<long>(),
                };
            }
        }

        foreach (var (from, to) in edges)
        {
            var fromId = MdMapGen.NodeId(floor, from.x + 1, from.y);
            var toId = MdMapGen.NodeId(floor, to.x + 1, to.y);
            if (!dict.ContainsKey(toId)) continue;
            if (dict.TryGetValue(fromId, out var fromNs)) fromNs.nnids.Add(toId);
        }

        var startNode = new Ns
        {
            f = floor,
            s = 0,
            nid = MdMapGen.NodeId(floor, 0, 0),
            e = 0,
            eid = 0,
            nnids = startingNodes.Select(c => MdMapGen.NodeId(floor, c.x + 1, c.y)).ToList(),
        };
        dict[startNode.nid] = startNode;

        return dict.Values.ToList();
    }
}

public static class MdMapGen
{
    public static long NodeId(long floor, long x, long y) => floor * 10000 + x * 100 + y;

    // Port of random_bool_vec. Rust's `rng.gen_range(a..=b)` is inclusive on both ends;
    // Random.Shared.Next(min, max) is exclusive on max, hence the `+ 1`.
    public static bool[] RandomBoolVec(int len, int weight, int offLo, int offHi)
    {
        var offset = Math.Max(weight + Random.Shared.Next(offLo, offHi + 1), 2);
        var bools = new bool[len];
        for (var i = 0; i < len; i++) bools[i] = i < offset;

        for (var i = len - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (bools[i], bools[j]) = (bools[j], bools[i]);
        }

        return bools;
    }

    // Port of DefaultMirrorDungeonMapGenerator::generate_battle_node.
    public static DungeonNode GenerateBattleNode(ThemeStatic theme)
    {
        var opt = theme.mapGenOption;
        while (true)
        {
            var p = Random.Shared.NextDouble();
            List<long> pool;
            long nodeType;
            if (p < 0.45) { pool = opt.battlePool; nodeType = 1; }
            else if (p < 0.70) { pool = opt.abBattlePool; nodeType = 5; }
            else if (p < 0.80) { pool = opt.eventPool; nodeType = 3; }
            else if (p < 0.90) { pool = opt.hardAbBattlePool; nodeType = 14; }
            else { pool = opt.hardBattlePool; nodeType = 2; }

            if (pool.Count == 0) continue;
            var id = pool[Random.Shared.Next(pool.Count)];
            return new DungeonNode { node_type = nodeType, encounter_id = id };
        }
    }

    // Port of DefaultMirrorDungeonMapGenerator::generate_md4_graph.
    public static DungeonGraph GenerateMd4Graph(ThemeStatic theme, long floor, int floorWidth)
    {
        var graph = new DungeonGraph();

        var layerPopulation = new[]
        {
            RandomBoolVec(floorWidth, floorWidth / 2, 0, 2),
            RandomBoolVec(floorWidth, floorWidth / 2, 1, 3),
            RandomBoolVec(floorWidth, floorWidth / 2, 0, 2),
        };

        for (var x = 0; x < floorWidth; x++)
        {
            for (var y = 0; y < 3; y++)
            {
                var middleNodeMissing = !layerPopulation[1][x] && y != 1;
                var lastMiddleNodeMissing = x > 0 && y == 1 && !layerPopulation[1][x - 1];
                var beginning = x == 0 && y != 1;
                if (layerPopulation[y][x] || middleNodeMissing || lastMiddleNodeMissing || beginning)
                {
                    graph.nodes[x, y] = GenerateBattleNode(theme);
                }
            }
        }

        for (var y = 0; y < 3; y++)
        {
            if (graph.nodes[0, y] is not null)
                graph.startingNodes.Add(new DungeonCoordinate(0, y));
        }

        var bossPool = theme.mapGenOption.bossPool;
        var bossEncounterId = bossPool.Count > 0 ? bossPool[Random.Shared.Next(bossPool.Count)] : 2060101;

        var superShopProb = floor switch
        {
            0 => 0.03,
            1 => 0.25,
            2 => 0.50,
            3 => 0.75,
            _ => 1.00,
        };
        graph.nodes[floorWidth, 1] = new DungeonNode
        {
            node_type = 10,
            encounter_id = Random.Shared.NextDouble() < superShopProb ? 1 : 0,
        };
        graph.nodes[floorWidth + 1, 1] = new DungeonNode { node_type = 6, encounter_id = bossEncounterId };

        graph.AddEdge(new DungeonCoordinate(floorWidth, 1), new DungeonCoordinate(floorWidth + 1, 1));

        for (var x = 0; x < floorWidth; x++)
        {
            for (var y = 0; y < 3; y++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    graph.AddEdge(new DungeonCoordinate(x, y), new DungeonCoordinate(x + 1, y + dy));
                }
            }
        }

        // Remove overlapping ("crossing") edges: for every edge that steps diagonally
        // down-right (x+1, y-1), if the crossing diagonal up-right edge also exists, drop
        // one of the two at random.
        var oldEdges = new HashSet<(DungeonCoordinate, DungeonCoordinate)>(graph.edges);
        foreach (var (from, to) in oldEdges)
        {
            if (from.x + 1 != to.x || from.y - 1 != to.y) continue;

            var crossEdge = (new DungeonCoordinate(from.x, from.y - 1), new DungeonCoordinate(from.x + 1, from.y));
            if (!graph.edges.Contains(crossEdge)) continue;

            if (Random.Shared.NextDouble() < 0.5)
                graph.edges.Remove(crossEdge);
            else
                graph.edges.Remove((from, to));
        }

        return graph;
    }

    // Port of DefaultMirrorDungeonMapGenerator::generate_new_floor. Throws if the theme id
    // is unknown (Rust returns diesel::NotFound); the handler layer maps that to a 500.
    public static void GenerateNewFloor(long floor, long themeId, MirrorOriginSaveInfo save)
    {
        var theme = MdThemeData.GetThemeById(themeId)
            ?? throw new KeyNotFoundException($"MD theme {themeId} not found");

        var seq = theme.mapGenSequence.FirstOrDefault();
        List<Ns> graph;

        if (seq?.type_ == "CREATE_EMPTY_MD4_FLOOR")
        {
            var floorWidth = seq.numberList.Count > 0
                ? (int)seq.numberList[0]
                : (int)(4 + floor / 3);
            graph = GenerateMd4Graph(theme, floor, floorWidth).EncodeAsMd(floor);
        }
        else if (seq?.type_ == "CREATE_FIXED_FLOOR")
        {
            graph = seq.nodeList.Select(node =>
            {
                var eid = node.encounterId;
                if (node.encounterType == 3 && node.encounterId == 0)
                {
                    var opt = theme.mapGenOption;
                    if (opt.specialEventPool is { } specialEvents && opt.specialEventProb is { } specialEventProb)
                    {
                        if (Random.Shared.NextDouble() < specialEventProb && specialEvents.Count > 0)
                        {
                            eid = specialEvents[Random.Shared.Next(specialEvents.Count)];
                        }
                    }

                    if (eid == 0 && opt.eventPool.Count > 0)
                    {
                        eid = opt.eventPool[Random.Shared.Next(opt.eventPool.Count)];
                    }
                }

                return new Ns
                {
                    f = floor,
                    s = node.sector,
                    nid = NodeId(floor, node.sector, node.idx),
                    e = node.encounterType,
                    eid = eid,
                    nnids = node.connectedNextNodeIdxList.Select(n => NodeId(floor, node.sector + 1, n)).ToList(),
                };
            }).ToList();
        }
        else
        {
            graph = new List<Ns>();
        }

        save.dungeonMap.ns.AddRange(graph);
        UpdateResult(save, floor, themeId);
    }

    // Port of update_result.
    public static void UpdateResult(MirrorOriginSaveInfo save, long floor, long selectedTheme)
    {
        if (floor != 0)
        {
            save.currentInfo.cn = new Currentnode { f = floor, s = 0, nid = NodeId(floor, 0, 0) };
        }

        save.currentInfo.eid = 0;

        var egoRewards = save.currentInfo.tfps.FirstOrDefault(t => t.tfid == selectedTheme)?.egs
            ?? new List<long>();

        save.currentInfo.tfps.Clear();

        save.currentInfo.tfs.Add(new Tfs { f = floor, tid = 0, idx = 0, tfid = selectedTheme, egs = egoRewards });

        save.isEndDungeon = 0;
        save.currentInfo.sepsCreated = 2;
        save.currentInfo.startKeyword = "Penetrate";
    }

    // Port of ThemeFloorPoolSelector::random_from_pool (ego_reward_count=4, theme_floor_count=4).
    public static List<Tfps> PickThemes(long floor)
    {
        var themePool = new MdThemePool();
        var pools = themePool.pools.Values
            .Where(t => t.exceptionConditions.Any(c => c.selectableFloors.Contains(floor)))
            .ToList();

        var easy = pools.Where(t => t.exceptionConditions.Any(c => c.dungeonIdx == 0)).ToList();
        var hard = pools.Where(t => t.exceptionConditions.Any(c => c.dungeonIdx == 1)).ToList();

        var result = new List<Tfps>();
        foreach (var theme in ChooseMultiple(easy, 4))
            result.Add(BuildTfps(themePool, theme, floor, hard: false));
        foreach (var theme in ChooseMultiple(hard, 4))
            result.Add(BuildTfps(themePool, theme, floor, hard: true));
        return result;
    }

    private static Tfps BuildTfps(MdThemePool pool, ThemeStatic theme, long floor, bool hard) => new()
    {
        tfid = theme.id,
        egs = pool.SelectRandomEgosFromPool(theme.id, 4, floor),
        idx = hard ? 1 : 0,
        upegs = new List<long> { 9051, 9048, 9001, 9066 },
    };

    // Port of Rust's `choose_multiple` usage - random k distinct elements, or all (shuffled)
    // if the source has k or fewer.
    public static List<T> ChooseMultiple<T>(IEnumerable<T> src, int k)
    {
        var list = src.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list.Take(Math.Min(k, list.Count)).ToList();
    }
}
