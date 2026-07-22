using System;
using System.Collections.Generic;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server;

// Port of lethe-server server/src/api/storymirrordungeon/story_map_gen/mod.rs. Shares
// DungeonGraph, MdMapGen.NodeId and MdMapGen.RandomBoolVec with the mirror-dungeon map
// generator (src/OpenLethe.Server/MdMapGen.cs) - verified identical between the two Rust
// files, reused here rather than duplicated. RNG uses Random.Shared (non-deterministic like
// Rust's thread_rng) - tests assert structural invariants only.
// See memory: mirror-dungeon-rng-is-nondeterministic.
public static class StoryMdMapGen
{
    // Port of DefaultStoryMirrorDungeonMapGenerator::generate_battle_node. FOUR branches
    // (story-MD has no hard_ab_battle_pool branch, unlike MD's five).
    // Keep the retry `loop` faithful to Rust - no iteration bound. It re-rolls `p` every
    // pass, and every story-MD dungeon's derived theme (Task 2) has a non-empty battlePool,
    // which alone guarantees eventual termination - enforced by the guard in
    // GenerateNewFloor below, which is the only path that reaches this loop.
    public static DungeonNode GenerateBattleNode(StoryMdTheme theme)
    {
        while (true)
        {
            var p = Random.Shared.NextDouble();
            List<long> pool;
            long nodeType;
            if (p < 0.45) { pool = theme.battlePool; nodeType = 1; }
            else if (p < 0.70) { pool = theme.abBattlePool; nodeType = 5; }
            else if (p < 0.90) { pool = theme.eventPool; nodeType = 3; }
            else { pool = theme.hardBattlePool; nodeType = 2; }

            if (pool.Count == 0) continue;
            var id = pool[Random.Shared.Next(pool.Count)];
            return new DungeonNode { node_type = nodeType, encounter_id = id };
        }
    }

    // Port of DefaultStoryMirrorDungeonMapGenerator::generate_md4_graph. Node population,
    // starting nodes, edge creation and cross-edge removal are identical to
    // MdMapGen.GenerateMd4Graph; but THREE tail nodes (rest, floor, boss) and no super-shop
    // probability roll - the rest node's encounter_id is always 0.
    public static DungeonGraph GenerateMd4Graph(StoryMdTheme theme, int floorWidth)
    {
        var graph = new DungeonGraph();

        var layerPopulation = new[]
        {
            MdMapGen.RandomBoolVec(floorWidth, floorWidth / 2, 0, 2),
            MdMapGen.RandomBoolVec(floorWidth, floorWidth / 2, 1, 3),
            MdMapGen.RandomBoolVec(floorWidth, floorWidth / 2, 0, 2),
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

        var bossPool = theme.bossPool;
        var bossEncounterId = bossPool.Count > 0 ? bossPool[Random.Shared.Next(bossPool.Count)] : 2060101;

        graph.nodes[floorWidth, 1] = new DungeonNode { node_type = 10, encounter_id = 0 };
        graph.nodes[floorWidth + 1, 1] = new DungeonNode { node_type = 11, encounter_id = 0 };
        graph.nodes[floorWidth + 2, 1] = new DungeonNode { node_type = 6, encounter_id = bossEncounterId };

        graph.AddEdge(new DungeonCoordinate(floorWidth, 1), new DungeonCoordinate(floorWidth + 1, 1));
        graph.AddEdge(new DungeonCoordinate(floorWidth + 1, 1), new DungeonCoordinate(floorWidth + 2, 1));

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

    // Port of DefaultStoryMirrorDungeonMapGenerator::generate_new_floor.
    // ponytail: Rust synthesizes theme_pool.map_gen_sequence as a single entry -
    // { type_field: "CREATE_EMPTY_MD4_FLOOR", number_list: [7, 4, 1] } - and reads only
    // `.first()`, so floor width is always the literal 7 below. Task 2 deliberately does not
    // model MapGenSequence since only this one hard-coded path is ever reachable. Rust's
    // sibling "CREATE_FIXED_FLOOR" match arm is therefore dead code and is intentionally not
    // ported here.
    public static void GenerateNewFloor(long floor, long dungeonId, StoryMirrorSaveInfo save)
    {
        var theme = StoryMdThemeData.GetTheme(dungeonId);

        // StoryMdThemeData.GetTheme is deliberately total (Task 2) and returns an all-empty
        // theme for unknown ids. GenerateBattleNode's retry loop is only bounded because
        // battlePool is guaranteed non-empty for every known story-MD dungeon; an unknown id
        // would spin forever. Fail fast here instead - the handler layer maps this to a 500,
        // matching MdMapGen.GenerateNewFloor's precedent for unknown MD theme ids.
        if (theme.battlePool.Count == 0)
            throw new KeyNotFoundException($"Story MD theme {dungeonId} not found");

        const int floorWidth = 7;

        var graph = GenerateMd4Graph(theme, floorWidth).EncodeAsMd(floor);
        save.map.ns.AddRange(graph);

        UpdateResult(save, floor);
    }

    // Port of update_result. Simpler than MD's UpdateResult - no theme-floor-pool bookkeeping.
    public static void UpdateResult(StoryMirrorSaveInfo save, long floor)
    {
        if (floor != 0)
        {
            save.currentinfo.cn = new Currentnode { f = floor, s = 0, nid = MdMapGen.NodeId(floor, 0, 0) };
        }

        save.currentinfo.eid = 0;
        save.currentinfo.sepsCreated = 2;
    }
}
