using System.Text.Json.Nodes;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server;

/// One dungeon's stored railway state. Railway state is per dungeonId - the
/// capture shows three saves (6, 1001, 1002) alive on one account at once - so
/// the RailwaySaveInfo column holds a dungeonId -> RailwayRun map.
public sealed class RailwayRun
{
    public RailwaySaveInfo save = new();
    public List<UpdateNodeDatas> nodes = new();
    public List<CurrentLog> logs = new();
}

/// Pure rules behind the railway handlers, all derived from the Refraction
/// Railway capture (docs/flows(2)). Handlers stay dispatch-only.
public static class RailwayRules
{
    /// Seeds are a plain roll; the capture's values all land under 1e8.
    public const int SeedBound = 100_000_000;

    public static string NowIso() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    // ---- list bookkeeping ----

    /// Replace the node with the same nodeid, else append.
    public static void UpsertNode(List<UpdateNodeDatas> nodes, UpdateNodeDatas newNode)
    {
        for (var i = 0; i < nodes.Count; i++)
            if (nodes[i].nodeid == newNode.nodeid) { nodes[i] = newNode; return; }
        nodes.Add(newNode);
    }

    /// Return the node with nodeId; if absent, append a fresh default and return it.
    public static UpdateNodeDatas FindOrDefaultNode(List<UpdateNodeDatas> nodes, long nodeId)
    {
        foreach (var n in nodes)
            if (n.nodeid == nodeId) return n;
        var made = new UpdateNodeDatas { nodeid = nodeId };
        nodes.Add(made);
        return made;
    }

    /// Replace the buff set with the same nid, else append.
    public static void UpsertBuff(List<Buffsetsbyegogift> buffs, Buffsetsbyegogift newBuff)
    {
        for (var i = 0; i < buffs.Count; i++)
            if (buffs[i].nid == newBuff.nid) { buffs[i] = newBuff; return; }
        buffs.Add(newBuff);
    }

    /// Buff sets whose nid is strictly less than nodeId.
    public static List<Buffsetsbyegogift> BuffsBelowNode(List<Buffsetsbyegogift> buffs, long nodeId) =>
        buffs.Where(b => b.nid < nodeId).ToList();

    // ---- extra rewards ----

    /// The extra-reward states a save should expose: one entry per statically
    /// defined reward whose conditions the run has met, keeping the acquired flag
    /// of any entry already stored. When the dungeon has no bundled definition
    /// the stored list is preserved verbatim rather than wiped - the account's
    /// real progress is not ours to delete just because we lack the data file.
    public static List<Extrarewardstate> UnlockedExtraRewards(RailwayRun run)
    {
        var defined = RailwayData.ExtraRewards(run.save.id);
        if (defined.Count == 0) return run.save.extrarewardstate;

        var acquired = run.save.extrarewardstate.Where(e => e.isRewarded).Select(e => e.id).ToHashSet();
        var bestTurn = run.logs.Count == 0 ? long.MaxValue : run.logs.Min(l => l.clearturn);
        var now = DateTime.UtcNow;

        return defined
            .Where(r => !Expired(r.endDate, now))
            .Where(r => r.requiredConditions.All(c => Satisfied(c, run.save, bestTurn)))
            .Select(r => new Extrarewardstate { id = r.id, isRewarded = acquired.Contains(r.id) })
            .ToList();
    }

    private static bool Expired(string? endDate, DateTime now) =>
        endDate is not null && DateTime.TryParse(endDate, null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var end) && end < now;

    private static bool Satisfied(RailwayCondition c, RailwaySaveInfo save, long bestTurn) => c.type switch
    {
        "CLEAR_NODE" => save.lastclearnode >= c.count,
        "ROTATION_COUNT" => save.lastclearrotation >= c.count,
        "CLEAR_TURN_COUNT" => bestTurn <= c.count,
        // ponytail: unknown condition types unlock. No other type occurs in any
        // bundled railway-dungeon file; revisit if a dump adds one.
        _ => true,
    };

    /// Mark every unlocked reward acquired and return the elements granted by the
    /// ones that were not already. The port has no item inventory, so granting is
    /// exactly "flip the flag and report what would have been given".
    public static List<Wire.Element> AcquireExtraRewards(RailwayRun run)
    {
        var granted = new List<Wire.Element>();
        var byId = RailwayData.ExtraRewards(run.save.id).ToDictionary(r => r.id);
        foreach (var state in run.save.extrarewardstate)
        {
            if (state.isRewarded) continue;
            state.isRewarded = true;
            if (byId.TryGetValue(state.id, out var def)) granted.AddRange(def.rewards);
        }
        return granted;
    }

    // ---- rotation buffs ----

    /// Record a rotation's buff picks. The client decides what to OFFER; the
    /// server only accumulates picks and maintains the per-set exclusion
    /// bookkeeping the client reads back when building the next offer:
    /// EXCLUDE_RECENT keeps `recentbuffid`, EXCLUDE_ACQUIRED_UNTIL_GET_ALL keeps
    /// `currentbuffids` until every id in the set's pool has been taken, then
    /// clears it. Both behaviours are byte-verified against the capture.
    public static void ApplyBuffSelection(RailwayRun run, IEnumerable<SelectedBuff> selected)
    {
        var defs = RailwayData.BuffSets(run.save.id).ToDictionary(b => b.buffSetId);

        foreach (var pick in selected)
        {
            var set = run.save.buffsets.FirstOrDefault(b => b.setid == pick.setId);
            if (set is null) { set = new Buffsets { setid = pick.setId }; run.save.buffsets.Add(set); }

            var buff = set.buffs.FirstOrDefault(b => b.id == pick.buffId);
            if (buff is null) { buff = new RailwayBuff { id = pick.buffId }; set.buffs.Add(buff); }
            buff.count++;
            if (pick.targetId != 0) buff.targetids.Add(pick.targetId);

            defs.TryGetValue(pick.setId, out var def);
            var options = def?.selectOption.Select(o => o.type).ToList() ?? new List<string>();
            if (options.Contains("EXCLUDE_ACQUIRED_UNTIL_GET_ALL"))
            {
                set.currentbuffids.Add(pick.buffId);
                if (def is not null && set.currentbuffids.Count >= def.idList.Count) set.currentbuffids.Clear();
            }
            else if (def is null || options.Contains("EXCLUDE_RECENT"))
            {
                set.recentbuffid = pick.buffId;
            }
        }

        run.save.currentclearrotation++;
        // A rotation ends with no node in progress; the capture resets this on
        // every buff pick, not on the node exit before it.
        run.save.currentnode = 0;
    }

    // ---- clear log ----

    /// Build the clear-record entry for a finished run from its cleared nodes.
    /// Every field is derived: the real server's log carries per-node unit info
    /// that our stored node `status` reproduces one-for-one (with `pord` zeroed,
    /// exactly as the capture's log entries do).
    public static CurrentLog BuildLog(RailwayRun run, long idx, string date)
    {
        var cleared = run.nodes.Where(n => n.nodeid != 0 && n.nodestate == 1).OrderBy(n => n.nodeid).ToList();
        var last = cleared.LastOrDefault();

        return new CurrentLog
        {
            idx = idx,
            personalities = last is null ? new() : UnitsFromStatus(last.status),
            statistics = SumStatistics(cleared),
            detailstatistics = DetailStatistics(run.save.id, cleared),
            clearturn = cleared.Sum(n => n.clearturn),
            turnspernode = cleared.Select(n => new Turnspernode { nid = n.nodeid, turn = n.clearturn }).ToList(),
            clearrotation = run.save.currentclearrotation,
            buffsets = run.save.buffsets,
            buffsetsbyegogift = run.save.buffsetsbyegogift,
            date = date,
            startdate = run.save.startdate,
            deadunitnumber = last is null ? 0 : last.status.Count(s => s.hp <= 0),
            prevclearnode = run.save.prevclearnode,
            currentnode = run.save.currentnode,
        };
    }

    /// Per-pid gd/rd totals over the given nodes, in first-seen order.
    private static List<Statistics1> SumStatistics(IEnumerable<UpdateNodeDatas> nodes)
    {
        var totals = new List<Statistics1>();
        var byPid = new Dictionary<long, Statistics1>();
        foreach (var stat in nodes.SelectMany(n => n.statistics))
        {
            if (!byPid.TryGetValue(stat.id, out var total))
            {
                total = new Statistics1 { id = stat.id };
                byPid[stat.id] = total;
                totals.Add(total);
            }
            total.gd += stat.gd;
            total.rd += stat.rd;
        }
        return totals;
    }

    /// One clear-record entry per node group. A dungeon that declares
    /// nodeIdCollection_ForLog (3, 4, 5, 6 and 1001 do) aggregates its nodes into
    /// those groups and reports the group's formation-target node's party;
    /// one that declares none (1, 2 and 1002) logs one entry per cleared node,
    /// keyed by node id - the shape the capture shows for 1002.
    private static List<Detailstatistics> DetailStatistics(long dungeonId, List<UpdateNodeDatas> cleared)
    {
        var collections = RailwayData.LogCollections(dungeonId);
        if (collections.Count == 0)
            return cleared.Select(n => new Detailstatistics
            {
                collectionId = n.nodeid,
                personalities = UnitsFromStatus(n.status),
                statistics = SumStatistics(new[] { n }),
            }).ToList();

        return collections
            .Select(c => (c, nodes: cleared.Where(n => c.nodeIds.Contains(n.nodeid)).ToList()))
            .Where(x => x.nodes.Count > 0)
            .Select(x => new Detailstatistics
            {
                collectionId = x.c.collectionId,
                personalities = UnitsFromStatus(
                    (x.nodes.FirstOrDefault(n => n.nodeid == x.c.formationTargetNode) ?? x.nodes[^1]).status),
                statistics = SumStatistics(x.nodes),
            }).ToList();
    }

    private static List<Personalities> UnitsFromStatus(List<PrevStatusData> status) =>
        status.Select(s => new Personalities
        {
            pid = s.pid, es = s.egos, g = s.g, l = s.lv, sp = s.sp, gi = s.gi, sid = s.sid, pord = 0,
        }).ToList();

    // ---- rest nodes ----

    /// Full health on the save's normalized unit-health scale (the same 0-10000
    /// scale MD's `ch` uses; every healthy unit in the capture reads 10000).
    public const long MaxHp = 10000;

    /// The party a rest node leaves behind. The request carries only the
    /// formation - who is in the party and their identity/EGO loadout - so the
    /// live condition (hp/mp/isp/sin/sp) is carried forward from the last cleared
    /// node and then recovered per the dungeon's static rest config:
    /// `hasRestHeal` gates an HP heal of `restHPHealRate` percent of max, and
    /// `isResetMPAtRestNode` / `restMPHeal` adjust MP independently of it.
    /// A unit with no carried status is joining fresh and starts at full.
    ///
    /// No capture exercises a rest node (Refraction Railway 2 has none), so this
    /// is derived from static config rather than replayed. It replaces a
    /// hardcoded full heal at level 60, which was right only for dungeon 1.
    public static List<PrevStatusData> RestNodeStatus(
        long dungeonId, List<Personalities> formation, List<PrevStatusData> carried)
    {
        var def = RailwayData.Find(dungeonId);
        var healed = new List<PrevStatusData>();

        foreach (var unit in formation)
        {
            var before = carried.FirstOrDefault(s => s.pid == unit.pid);
            var status = new PrevStatusData
            {
                pid = unit.pid,
                // identity + loadout come from the formation the client just sent
                egos = unit.es, lv = unit.l, g = unit.g, gi = unit.gi, sid = unit.sid, pord = unit.pord,
                // live condition carries over from the last fight
                hp = before?.hp ?? MaxHp,
                mp = before?.mp ?? 0,
                isp = before?.isp ?? 0,
                sp = before?.sp ?? 0,
                sin = before?.sin ?? new Sin(),
            };

            // ponytail: a downed unit (hp <= 0) is not healed back up - reviving is
            // its own thing upstream (isReviveAllAfterWin/isRecoverAllAfterWin are
            // separate flags). Drop this guard if a capture ever shows otherwise.
            if (def is { hasRestHeal: true } && status.hp > 0)
                status.hp = Math.Min(MaxHp, status.hp + MaxHp * def.restHPHealRate / 100);

            if (def is { isResetMPAtRestNode: true }) status.mp = 0;
            status.mp += def?.restMPHeal ?? 0;

            healed.Add(status);
        }
        return healed;
    }

    // ---- misc ----

    /// The real server echoes `{}` for a node's enemy save when the client sent
    /// an empty one, so normalize instead of round-tripping the zero-filled
    /// object the client posts.
    public static JsonNode NormalizeEnemy(JsonNode? enemy)
    {
        if (enemy is not JsonObject o) return new JsonObject();
        var wave = (long?)o["lastWave"] ?? 0;
        var turn = (long?)o["lastTurn"] ?? 0;
        var abnos = (o["abnoSaveDataList"] as JsonArray)?.Count ?? 0;
        return wave == 0 && turn == 0 && abnos == 0 ? new JsonObject() : o.DeepClone();
    }
}
