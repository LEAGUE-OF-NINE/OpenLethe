using System.Collections.Generic;
using System.Linq;
using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.MirrorDungeon.Rules;

// Update-map-node / battle-after-choice rules. UpdateNode DELEGATES to the shared
// OpenLethe.Server.MdEventManager engine (used by Story-MD too - not moved, not modified) via a
// Run-facing IDungeonEventSave adapter (RunEventSave, the domain twin of MdEventSave/
// StoryEventSave/StoryMdEventSave). Verbatim ports of Handlers/MirrorDungeonEvents.cs
// UpdateMirrorDungeonMapNode and the (stateless) EnterMirrordungeonMapNodeBattleAfterChoice body
// in Handlers/MirrorDungeonMap.cs. See the handler-migration Task 11 brief.
public static class EventEngine
{
    // Run-facing IDungeonEventSave - domain twin of MdEventSave (MdEventManager.cs), which
    // adapts the same engine onto the wire MirrorOriginSaveInfo. Same field mapping, just onto
    // Run/PartyUnit instead of save.currentInfo/Dungeonunitlist1.
    private sealed class RunEventSave : OpenLethe.Server.IDungeonEventSave
    {
        private readonly Run _run;
        public RunEventSave(Run run) => _run = run;

        public void PushEgoGift(long rewardId) => _run.Gifts.Items.Add(new EgoGift { Id = rewardId });

        public Dictionary<long, OpenLethe.Server.UnitStats> GetUnitStats()
        {
            var stats = new Dictionary<long, OpenLethe.Server.UnitStats>();
            foreach (var unit in _run.Party)
                stats[unit.PersonalityId] = new OpenLethe.Server.UnitStats { hp = unit.CurrentHp, sp = unit.Cm }; // last-wins on dup pid
            return stats;
        }

        public void SetUnitStats(Dictionary<long, OpenLethe.Server.UnitStats> stats)
        {
            foreach (var unit in _run.Party)
                if (stats.TryGetValue(unit.PersonalityId, out var s)) { unit.CurrentHp = s.hp; unit.Cm = s.sp; }
        }

        public void AddCost(long cost) => _run.Cost += cost;
    }

    // Verbatim port of UpdateMirrorDungeonMapNode (Handlers/MirrorDungeonEvents.cs :26-96).
    // Throws KeyNotFoundException when the eid can't be resolved (mirrors the handler's
    // `return Results.StatusCode(500)` there - the caller maps the exception the same way
    // RewardResolution.AcquireReward's KeyNotFoundException is mapped by its handler).
    public static void UpdateNode(Run run, Currentnode currentNode, ChoiceEventData choiceEventData, List<Dungeonunitlist1> dungeonUnitList)
    {
        // Rust: pce.first().and_then(|e| e.nei).or_else(|| dungeonMap.ns node lookup).
        // ponytail: FirstOrDefault is only the still-open chain head while pce.Count <= 2 (true
        // for every record in the 321-record md-extreme capture) - see the wire twin's comment.
        long? eid = null;
        var pe = run.ChoiceEvents.FirstOrDefault();
        if (pe is not null && pe.Nei.HasValue) eid = pe.Nei.Value;
        if (eid is null)
            eid = run.Floor.Nodes.FirstOrDefault(n => n.Nid == currentNode.nid)?.Eid;
        if (eid is null) throw new KeyNotFoundException($"UpdateNode: could not resolve eid for nid {currentNode.nid}");

        long choiceIdx = choiceEventData.sl.Count > 0 ? choiceEventData.sl[0] : 0;
        long cs = choiceEventData.cs;

        // Confirmed-gift deferral: normal 9xxx gifts a GetConfirmedEgogift result form pushes are
        // QUEUED - stripped back out here, realized later by ExitMapNode's e==3 branch (which
        // re-derives them from the pce chain) - while hidden 993xxx level-cap gifts land
        // immediately. Snapshot the Gifts tail, run the event, then strip back out whatever
        // ProcessEvent just pushed that isn't a hidden gift.
        var egsBefore = run.Gifts.Items.Count;
        long next = OpenLethe.Server.MdEventManager.ProcessEvent(
            eid.Value, OpenLethe.Server.MdEventManager.ClampChoiceIndex(choiceIdx), cs, new RunEventSave(run));
        var pushed = run.Gifts.Items.GetRange(egsBefore, run.Gifts.Items.Count - egsBefore);
        run.Gifts.Items.RemoveRange(egsBefore, pushed.Count);
        run.Gifts.Items.AddRange(pushed.Where(g => Effects.IsHiddenConfirmedGift(g.Id)));

        // Merge the client's roster onto the server-authoritative party AFTER the event engine
        // runs (same rule as ExitMapNode/RewardEngine - SharedRules.MergeParty). The capture
        // always echoes the client's own ch/cm verbatim - client-reported stats win over whatever
        // ApplyHpSp computed, so the merge must run last and overwrite them.
        var incomingParty = dungeonUnitList.Select(u => new PartyUnit
        {
            PersonalityId = u.pid, CurrentHp = u.ch, Cm = u.cm, Mhos = u.mhos,
            Gacksung = u.g, Level = u.l, Isp = u.isp, Sid = u.sid, Mlos = u.mlos,
            Pord = u.pord,
            UpgradeIndices = u.upidx,
            EgoSkills = u.es.Select(e => new EgoSkill { Id = e.id, G = e.g, Idx = e.idx }).ToList(),
        }).ToList();
        run.Party = SharedRules.MergeParty(run.Party, incomingParty);

        // A PushEgoGift may have granted a hidden level-cap gift THIS call - bump egmlos by a
        // DELTA of just the hidden gifts pushed now, not a wholesale SharedRules.RecomputeEgmlos
        // (deep-floor egmlos isn't a pure owned-gift sum - floor clears bump it with no gift in
        // Gifts - see RewardEngine/ExitMapNode). On shallow floors this equals the wholesale sum.
        run.LevelOffsets.Egmlos += pushed
            .Where(g => Effects.IsHiddenConfirmedGift(g.Id))
            .Sum(g => Effects.HiddenGiftLevelBump(g.Id));
        var levelCap = run.LevelOffsets.Sbmlos + run.LevelOffsets.Egmlos;
        foreach (var u in run.Party) u.Mlos = levelCap;

        // Append at the tail (oldest-first) - the eid-resolution pce.first() lookup above depends
        // on that ordering to find the still-active chain head across a multi-step event.
        // sl/cs/ri echo the request's choiceEventData verbatim - only Nei is engine-derived.
        run.ChoiceEvents.Add(new ChoiceEvent
        {
            Sl = new List<long>(choiceEventData.sl),
            Cs = choiceEventData.cs,
            Ri = choiceEventData.ri,
            Nei = next,
        });
    }

    // Verbatim port of the EnterMirrordungeonMapNodeBattleAfterChoice handler body
    // (Handlers/MirrorDungeonMap.cs :294-312). Stateless (no Run involved - the handler this
    // replaces never loads a save either; see its own comment: "nothing in the save is read or
    // mutated, capture-verified"), so no Run parameter is threaded through here (YAGNI - same
    // precedent as GetMirrorDungeonEgoGiftRecord, which also skips a Rules call entirely for a
    // pure static-data read).
    //
    // One log per requested abno id, in REQUEST order (NOT sorted by id): run-2 seq16 requests
    // [8585, 8200] and the response keeps that order - run-1's requests happened to be ascending,
    // so an OrderBy would match them by coincidence but diverge on run-2. Distinct() de-dupes
    // defensively (preserving first-occurrence order); all captured requests carry unique ids.
    public static List<AbnormalityLogEntry> BattleAfterChoice(List<long> abnormalityIds) =>
        abnormalityIds.Distinct().Select(id => new AbnormalityLogEntry
        {
            id = id,
            // k/s/p are per-battle RNG rolls (masked in the replay) - placeholder defaults.
            k = 0,
            s = new(),
            p = new(),
            ps = MdAbnoUnits.PartsFor(id).Select(partId => new AbnormalityLogPart
            {
                id = partId,
                // atrr/atkr are random resistance/attack permutations (masked) - placeholders.
                atrr = new(),
                atkr = new(),
            }).ToList(),
        }).ToList();
}
