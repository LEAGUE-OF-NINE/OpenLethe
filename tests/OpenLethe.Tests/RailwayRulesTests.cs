using System.Text.Json.Nodes;
using OpenLethe.Server;
using OpenLethe.Server.Wire;

public class RailwayRulesTests
{
    private static RailwayRun RunFor(long dungeonId) => new() { save = { id = dungeonId } };

    [Fact]
    public void UpsertNode_ReplacesSameNodeId_ElseAppends()
    {
        var nodes = new List<UpdateNodeDatas> { new() { nodeid = 1, clearturn = 5 } };
        RailwayRules.UpsertNode(nodes, new UpdateNodeDatas { nodeid = 1, clearturn = 9 });
        Assert.Single(nodes);
        Assert.Equal(9, nodes[0].clearturn);

        RailwayRules.UpsertNode(nodes, new UpdateNodeDatas { nodeid = 2 });
        Assert.Equal(2, nodes.Count);
    }

    [Fact]
    public void FindOrDefaultNode_ReturnsExisting_OrAppendsDefault()
    {
        var nodes = new List<UpdateNodeDatas> { new() { nodeid = 1 } };
        Assert.Same(nodes[0], RailwayRules.FindOrDefaultNode(nodes, 1));

        var made = RailwayRules.FindOrDefaultNode(nodes, 7);
        Assert.Equal(7, made.nodeid);
        Assert.Same(nodes[1], made);
    }

    [Fact]
    public void BuffsBelowNode_FiltersNidStrictlyLess()
    {
        var buffs = new List<Buffsetsbyegogift> { new() { nid = 0 }, new() { nid = 1 }, new() { nid = 2 } };
        Assert.Equal(new long[] { 0, 1 }, RailwayRules.BuffsBelowNode(buffs, 2).Select(b => b.nid).ToArray());
    }

    // Refraction Railway 2's line-2 buff sets: set 1 is EXCLUDE_RECENT, set 3 is
    // EXCLUDE_ACQUIRED_UNTIL_GET_ALL over a 5-id pool. Both traces below are the
    // capture's own five rotations (docs/flows(2) seq 108-135).
    [Fact]
    public void ApplyBuffSelection_TracksRecentAndUntilGetAll()
    {
        var run = RunFor(1002);
        var picks = new[] { (1L, 4L), (3L, 13L), (1L, 1L), (3L, 16L), (1L, 4L), (3L, 15L), (1L, 1L), (3L, 17L), (1L, 4L), (3L, 14L) };
        for (var i = 0; i < picks.Length; i += 2)
        {
            RailwayRules.ApplyBuffSelection(run, new[]
            {
                new SelectedBuff { setId = picks[i].Item1, buffId = picks[i].Item2 },
                new SelectedBuff { setId = picks[i + 1].Item1, buffId = picks[i + 1].Item2 },
            });
        }

        Assert.Equal(5, run.save.currentclearrotation);
        Assert.Equal(0, run.save.currentnode);

        var ally = run.save.buffsets.Single(b => b.setid == 1);
        Assert.Equal(4, ally.recentbuffid);                       // EXCLUDE_RECENT
        Assert.Empty(ally.currentbuffids);
        Assert.Equal(3, ally.buffs.Single(b => b.id == 4).count);  // picked 3x
        Assert.Equal(2, ally.buffs.Single(b => b.id == 1).count);

        var enemy = run.save.buffsets.Single(b => b.setid == 3);
        Assert.Equal(0, enemy.recentbuffid);
        Assert.Equal(5, enemy.buffs.Count);
        Assert.Empty(enemy.currentbuffids);                        // pool exhausted -> reset
    }

    [Fact]
    public void ApplyBuffSelection_UntilGetAll_AccumulatesUntilPoolExhausted()
    {
        var run = RunFor(1002);
        RailwayRules.ApplyBuffSelection(run, new[] { new SelectedBuff { setId = 3, buffId = 13 } });
        RailwayRules.ApplyBuffSelection(run, new[] { new SelectedBuff { setId = 3, buffId = 16 } });
        Assert.Equal(new long[] { 13, 16 }, run.save.buffsets.Single().currentbuffids.ToArray());
    }

    [Fact]
    public void UnlockedExtraRewards_TracksLastClearNode_KeepingAcquiredFlags()
    {
        // Railway 1001's 13 extra rewards are one per node (CLEAR_NODE 1..13).
        var run = RunFor(1001);
        run.save.lastclearnode = 3;
        run.save.extrarewardstate = RailwayRules.UnlockedExtraRewards(run);
        Assert.Equal(new long[] { 1, 2, 3 }, run.save.extrarewardstate.Select(e => e.id).ToArray());
        Assert.All(run.save.extrarewardstate, e => Assert.False(e.isRewarded));

        var granted = RailwayRules.AcquireExtraRewards(run);
        Assert.All(run.save.extrarewardstate, e => Assert.True(e.isRewarded));
        Assert.NotEmpty(granted);
        Assert.Empty(RailwayRules.AcquireExtraRewards(run)); // nothing left to grant

        run.save.lastclearnode = 5;
        run.save.extrarewardstate = RailwayRules.UnlockedExtraRewards(run);
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, run.save.extrarewardstate.Select(e => e.id).ToArray());
        Assert.Equal(3, run.save.extrarewardstate.Count(e => e.isRewarded));
    }

    [Fact]
    public void UnlockedExtraRewards_UndefinedDungeon_PreservesStoredProgress()
    {
        // Refraction Railway 2's rerun has no bundled railway-dungeon-1002.json;
        // an account's real reward progress must survive that rather than be wiped.
        var run = RunFor(1002);
        run.save.lastclearnode = 7;
        run.save.extrarewardstate = new List<Extrarewardstate> { new() { id = 1, isRewarded = true } };
        Assert.Equal(new long[] { 1 }, RailwayRules.UnlockedExtraRewards(run).Select(e => e.id).ToArray());
    }

    [Fact]
    public void BuildLog_DerivesTotalsAndPerNodeDetailFromClearedNodes()
    {
        var run = RunFor(1002);
        run.save.currentclearrotation = 2;
        run.save.prevclearnode = 3;
        run.save.currentnode = 3;
        run.save.startdate = "2026-08-14T16:09:29.000Z";
        run.nodes =
        [
            new UpdateNodeDatas { nodeid = 0, nodestate = 1 },
            new UpdateNodeDatas
            {
                nodeid = 1, nodestate = 1, clearturn = 10,
                status = [new PrevStatusData { pid = 7, lv = 55, g = 3, pord = 2, hp = 10000 }],
                statistics = [new Statistics1 { id = 7, gd = 100, rd = 5 }],
            },
            new UpdateNodeDatas { nodeid = 2, nodestate = 0 },   // never cleared
            new UpdateNodeDatas
            {
                nodeid = 3, nodestate = 1, clearturn = 12,
                status = [new PrevStatusData { pid = 7, lv = 55, g = 3, pord = 4, hp = 0 }],
                statistics = [new Statistics1 { id = 7, gd = 50, rd = 3 }],
            },
        ];

        var log = RailwayRules.BuildLog(run, 1, "2026-08-14T18:13:17.013Z");

        Assert.Equal(22, log.clearturn);
        Assert.Equal(new long[] { 1, 3 }, log.turnspernode.Select(t => t.nid).ToArray());
        Assert.Equal(new long[] { 1, 3 }, log.detailstatistics.Select(d => d.collectionId).ToArray());
        Assert.Equal(150, log.statistics.Single().gd);
        Assert.Equal(8, log.statistics.Single().rd);
        Assert.Equal(2, log.clearrotation);
        Assert.Equal(3, log.prevclearnode);
        Assert.Equal("2026-08-14T16:09:29.000Z", log.startdate);
        Assert.Equal(1, log.deadunitnumber);
        // log units come from the LAST cleared node's status, with pord zeroed
        Assert.Equal(55, log.personalities.Single().l);
        Assert.Equal(0, log.personalities.Single().pord);
    }

    // Railway 3 declares nodeIdCollection_ForLog: nodes 1-3 -> collection 1
    // (formationTargetNode 3), 5-7 -> 2, 9-11 -> 3, 13 -> 4. The clear record must
    // aggregate into those groups, not report one entry per node.
    [Fact]
    public void BuildLog_GroupsDetailByNodeCollection_WhenTheDungeonDeclaresOne()
    {
        var run = RunFor(3);
        run.nodes = new[] { 1L, 2L, 3L, 5L }.Select(id => new UpdateNodeDatas
        {
            nodeid = id, nodestate = 1, clearturn = 10,
            status = [new PrevStatusData { pid = 7, lv = (int)id }],
            statistics = [new Statistics1 { id = 7, gd = 100, rd = 5 }],
        }).ToList();

        var log = RailwayRules.BuildLog(run, 1, "d");

        Assert.Equal(new long[] { 1, 2 }, log.detailstatistics.Select(d => d.collectionId).ToArray());
        var first = log.detailstatistics[0];
        Assert.Equal(300, first.statistics.Single().gd);          // nodes 1+2+3 summed
        Assert.Equal(3, first.personalities.Single().l);           // formationTargetNode 3's party
        Assert.Equal(100, log.detailstatistics[1].statistics.Single().gd);  // only node 5 cleared
        // turnspernode stays per node, and the totals cover every cleared node
        Assert.Equal(4, log.turnspernode.Count);
        Assert.Equal(400, log.statistics.Single().gd);
    }

    private static List<Personalities> Party => [new Personalities { pid = 7, l = 55, g = 3, gi = 1, sid = 2, pord = 4 }];

    private static List<PrevStatusData> Wounded(long hp, long mp) =>
        [new PrevStatusData { pid = 7, hp = hp, mp = mp, isp = 6, sp = 9, lv = 1, pord = 99 }];

    // Dungeon 1: hasRestHeal, restHPHealRate 100, no MP reset -> full heal, MP kept.
    [Fact]
    public void RestNodeStatus_FullHealDungeon_RestoresHpAndKeepsMp()
    {
        var unit = RailwayRules.RestNodeStatus(1, Party, Wounded(hp: 3000, mp: 4)).Single();
        Assert.Equal(RailwayRules.MaxHp, unit.hp);
        Assert.Equal(4, unit.mp);
        // identity + loadout come from the formation, live condition carries over
        Assert.Equal(55, unit.lv);
        Assert.Equal(4, unit.pord);
        Assert.Equal(2, unit.sid);
        Assert.Equal(6, unit.isp);
        Assert.Equal(9, unit.sp);
    }

    // Dungeons 4 and 5: hasRestHeal, restHPHealRate 10, isResetMPAtRestNode.
    [Fact]
    public void RestNodeStatus_PartialHealDungeon_HealsTenPercentAndResetsMp()
    {
        var unit = RailwayRules.RestNodeStatus(4, Party, Wounded(hp: 3000, mp: 4)).Single();
        Assert.Equal(4000, unit.hp);
        Assert.Equal(0, unit.mp);

        // and the heal never overshoots full
        Assert.Equal(RailwayRules.MaxHp, RailwayRules.RestNodeStatus(4, Party, Wounded(9500, 0)).Single().hp);
    }

    // Dungeon 1001: restHPHealRate 100 but hasRestHeal FALSE -> no HP heal, MP still reset.
    [Fact]
    public void RestNodeStatus_HealRateIsGatedByHasRestHeal()
    {
        var unit = RailwayRules.RestNodeStatus(1001, Party, Wounded(hp: 3000, mp: 4)).Single();
        Assert.Equal(3000, unit.hp);
        Assert.Equal(0, unit.mp);
    }

    // Dungeons 2, 3, 6 (and any dungeon with no definition) recover nothing.
    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(999999)]
    public void RestNodeStatus_NoRestRecovery_CarriesConditionThrough(long dungeonId)
    {
        var unit = RailwayRules.RestNodeStatus(dungeonId, Party, Wounded(hp: 3000, mp: 4)).Single();
        Assert.Equal(3000, unit.hp);
        Assert.Equal(4, unit.mp);
    }

    [Fact]
    public void RestNodeStatus_DownedUnitIsNotHealed_AndAJoiningUnitStartsFull()
    {
        Assert.Equal(0, RailwayRules.RestNodeStatus(1, Party, Wounded(hp: 0, mp: 0)).Single().hp);
        // nobody carried forward -> the unit is joining fresh at this node
        Assert.Equal(RailwayRules.MaxHp, RailwayRules.RestNodeStatus(1, Party, new()).Single().hp);
    }

    [Fact]
    public void NormalizeEnemy_EmptySaveBecomesEmptyObject_PopulatedSurvives()
    {
        var empty = JsonNode.Parse("""{"lastWave":0,"lastTurn":0,"abnoSaveDataList":[]}""");
        Assert.Equal("{}", RailwayRules.NormalizeEnemy(empty).ToJsonString());
        Assert.Equal("{}", RailwayRules.NormalizeEnemy(null).ToJsonString());

        var real = JsonNode.Parse("""{"lastWave":2,"lastTurn":3,"abnoSaveDataList":[]}""");
        Assert.Equal(2, (long)RailwayRules.NormalizeEnemy(real)["lastWave"]!);
    }
}
