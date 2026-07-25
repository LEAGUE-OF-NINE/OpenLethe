using System;
using System.Collections.Generic;
using System.Linq;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server;

// Port of lethe-server server/src/api/md/events/mod.rs (EventManager). Engine is built
// interface-generic over IDungeonEventSave so a story-dungeon adapter can be added later
// without touching this file (only the MD save is wired in this cycle).

public sealed class UnitStats
{
    public long hp;
    public long sp;
}

public interface IDungeonEventSave
{
    void PushEgoGift(long rewardId);
    Dictionary<long, UnitStats> GetUnitStats();
    void SetUnitStats(Dictionary<long, UnitStats> stats);
    void AddCost(long cost);
}

// Port of the Rust `impl DungeonSaveInfo for MirrorOriginSaveInfo`.
public sealed class MdEventSave : IDungeonEventSave
{
    private readonly MirrorOriginSaveInfo _save;

    public MdEventSave(MirrorOriginSaveInfo save) => _save = save;

    public void PushEgoGift(long rewardId) =>
        _save.currentInfo.egs.Add(new AcquiredEgogifts { id = rewardId });

    public Dictionary<long, UnitStats> GetUnitStats()
    {
        var stats = new Dictionary<long, UnitStats>();
        foreach (var unit in _save.currentInfo.dul)
        {
            stats[unit.pid] = new UnitStats { hp = unit.ch, sp = unit.cm }; // last-wins on dup pid
        }
        return stats;
    }

    public void SetUnitStats(Dictionary<long, UnitStats> stats)
    {
        foreach (var unit in _save.currentInfo.dul)
        {
            if (stats.TryGetValue(unit.pid, out var stat))
            {
                unit.ch = stat.hp;
                unit.cm = stat.sp;
            }
        }
    }

    public void AddCost(long cost) => _save.currentInfo.cost += cost;
}

// Port of the Rust `impl DungeonSaveInfo for StorySaveInfo` (events/mod.rs:64-95).
public sealed class StoryEventSave : IDungeonEventSave
{
    private readonly StorySaveInfo _save;

    public StoryEventSave(StorySaveInfo save) => _save = save;

    public void PushEgoGift(long rewardId) =>
        _save.currentinfo.egs.Add(new AcquiredEgogifts { id = rewardId });

    public Dictionary<long, UnitStats> GetUnitStats()
    {
        var stats = new Dictionary<long, UnitStats>();
        foreach (var unit in _save.currentinfo.dul)
        {
            stats[unit.pid] = new UnitStats { hp = unit.ch, sp = unit.cm }; // last-wins on dup pid
        }
        return stats;
    }

    public void SetUnitStats(Dictionary<long, UnitStats> stats)
    {
        foreach (var unit in _save.currentinfo.dul)
        {
            if (stats.TryGetValue(unit.pid, out var stat))
            {
                unit.ch = stat.hp;
                unit.cm = stat.sp;
            }
        }
    }

    // Rust events/mod.rs:94 is `fn add_cost(&mut self, _: i64) {}` - the story-dungeon
    // save has no cost field. Intentionally does nothing.
    public void AddCost(long cost) { }
}

// Port of the Rust `impl DungeonSaveInfo for StoryMirrorSaveInfo` (events/mod.rs:97-128).
public sealed class StoryMdEventSave : IDungeonEventSave
{
    private readonly StoryMirrorSaveInfo _save;

    public StoryMdEventSave(StoryMirrorSaveInfo save) => _save = save;

    public void PushEgoGift(long rewardId) =>
        _save.currentinfo.egs.Add(new AcquiredEgogifts { id = rewardId });

    public Dictionary<long, UnitStats> GetUnitStats()
    {
        var stats = new Dictionary<long, UnitStats>();
        foreach (var unit in _save.currentinfo.dul)
        {
            stats[unit.pid] = new UnitStats { hp = unit.ch, sp = unit.cm }; // last-wins on dup pid
        }
        return stats;
    }

    public void SetUnitStats(Dictionary<long, UnitStats> stats)
    {
        foreach (var unit in _save.currentinfo.dul)
        {
            if (stats.TryGetValue(unit.pid, out var stat))
            {
                unit.ch = stat.hp;
                unit.cm = stat.sp;
            }
        }
    }

    // Rust events/mod.rs:127 is `fn add_cost(&mut self, _: i64) {}`. Currentinfo1 DOES have
    // a cost field, but the Rust impl deliberately ignores it. Intentionally does nothing.
    public void AddCost(long cost) { }
}

public static class MdEventManager
{
    /// Narrows a client-supplied long choice index to int for ProcessEvent. A naked
    /// (int) cast WRAPS on overflow (e.g. 4294967296 -> 0), turning a hostile
    /// out-of-range index into what looks like valid option 0. Rust's `as usize` does
    /// not wrap this way, so an out-of-range value stays out-of-range there and is
    /// rejected by the eachOptionList/eventResults bounds check. int.MaxValue is
    /// guaranteed larger than any real option list, so clamping to it lands a hostile
    /// value in that same "rejected" bucket instead of wrapping into a chosen option.
    public static int ClampChoiceIndex(long choiceIdx) =>
        choiceIdx is >= 0 and <= int.MaxValue ? (int)choiceIdx : int.MaxValue;

    /// Processes the event and returns the next event id (-1 = none/not found).
    public static long ProcessEvent(long eid, int choiceIdx, long coinState, IDungeonEventSave save)
    {
        var ev = MdEventData.GetById(eid);
        if (ev is null) return -1;
        if (ev.actionEvent is not null) return UpdateActionEventRewards(ev.actionEvent, choiceIdx, save);
        if (ev.personalityEvent is not null) return UpdatePersonalityEventReward(ev.personalityEvent, choiceIdx, coinState, save);
        return -1;
    }

    public static long UpdateActionEventRewards(ActionEvent ev, int choiceIdx, IDungeonEventSave save)
    {
        long nid = -1;
        if (choiceIdx >= 0 && choiceIdx < ev.eachOptionList.Count)
        {
            var option = ev.eachOptionList[choiceIdx];
            var result = PickWeightedResult(option.resultList, r => r.resultCondition);
            if (result is not null)
            {
                if (result.nextEventID.HasValue) nid = result.nextEventID.Value;

                if (result.eventResultDataList is not null)
                {
                    foreach (var erd in result.eventResultDataList)
                    {
                        var r = ApplyResultForm(erd.resultForm, null, save);
                        if (r.HasValue) nid = r.Value;
                    }
                }
            }
        }

        return nid;
    }

    public static long UpdatePersonalityEventReward(PersonalityEvent ev, int chosenPersonality, long coinState, IDungeonEventSave save)
    {
        long nextId = -1;
        if (coinState < 0 || coinState >= ev.eventResults.Count) return -1;
        var er = ev.eventResults[(int)coinState];

        var dataList = PickWeightedResult(er.eventResultDataList, d => d.resultCondition);
        if (dataList is not null)
        {
            nextId = dataList.nextEventID;

            if (dataList.eventResultDataList is not null)
            {
                foreach (var wrapper in dataList.eventResultDataList)
                {
                    var r = ApplyResultForm(wrapper.resultForm, chosenPersonality, save);
                    if (r.HasValue) nextId = r.Value;
                }
            }
        }

        return nextId;
    }

    /// Picks ONE entry from a resultList/eventResultDataList by weighted-random over each
    /// entry's `resultCondition` ("Prob_X" -> weight X; anything else defaults to weight 1).
    /// Discovered while wiring Task 11's exit-time chain replay: static data 97103601 has
    /// sibling entries ["Prob_1", "Prob_0.5"] (weights don't sum to 1 - a weighted table, not
    /// independent per-entry rolls) and 97103701 has ["Prob_0.5", "Prob_0.5"]; the md-extreme
    /// capture shows exactly ONE entry's gift fires in each case (seq283 grants only the
    /// Prob_1 entry's 9762, never also the Prob_0.5 entry's 9763; seq279 grants only ONE of
    /// 97103701's two 50/50 gifts) - never zero, never both. The prior code applied every
    /// entry in the list unconditionally, which was fine for the common single-entry
    /// (guaranteed) case but double-granted gifts for these two. Same MD-RNG non-determinism
    /// as the memory note "mirror dungeon RNG is non-deterministic" - Random.Shared, not a
    /// golden pick.
    /// ponytail: non-"Prob_" conditions (MpAverage_*, Failed_*, ProbTimesRepeatCount_*) exist
    /// in static data but aren't reachable by any of Task 11's 18 e==3 target chains - they
    /// fall back to equal weight (1) rather than their real state-dependent semantics. Extend
    /// ParseResultWeight when a covered record needs one modeled properly.
    private static T? PickWeightedResult<T>(List<T> items, Func<T, string?> condition) where T : class
    {
        if (items.Count == 0) return null;
        if (items.Count == 1) return items[0];

        var weights = items.Select(i => ParseResultWeight(condition(i))).ToArray();
        var total = weights.Sum();
        if (total <= 0) return items[0];

        var roll = Random.Shared.NextDouble() * total;
        var cumulative = 0.0;
        for (var i = 0; i < items.Count; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative) return items[i];
        }
        return items[^1];
    }

    private static double ParseResultWeight(string? resultCondition)
    {
        if (resultCondition is null) return 1.0;
        var parts = resultCondition.Split('_');
        if (parts.Length == 2 && parts[0] == "Prob" &&
            double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p))
            return p;
        return 1.0;
    }

    /// Port of apply_result_form. Rust's `target?` / `hp?` / `mp?` short-circuit the whole
    /// function back to `nid` (None unless the GetConfirmedEgogift next_battle_id branch set
    /// it) as soon as the target is missing or a number fails to parse - mirrored here as an
    /// early `return nid` per arm before any mutation happens.
    public static long? ApplyResultForm(ResultForm rf, long? chosenPersonality, IDungeonEventSave save)
    {
        var parts = rf.resultEffect.Split('_');
        var first = parts.Length > 0 ? parts[0] : "";
        var numbers = parts.Skip(1).ToArray();
        var target = rf.resultEffectTarget;

        long? nid = null;

        switch (first)
        {
            // GetConfirmedEgogiftOnWinBattle is a THIRD, distinct resultEffect literal (no
            // underscore, so `first` is the whole string) used by action-event options that
            // pair a StartBattle_Abnormality entry with the gift grant (e.g. static data event
            // 901021 option 1: nextBattleID 2060008 + two GetConfirmedEgogiftOnWinBattle
            // entries for gifts 9021/9134 - md-extreme fixture seq20->22). Was missing from
            // this switch entirely (fell through to the no-op default), so PushEgoGift never
            // ran for any action-event confirmed gift - root-caused while wiring Task 11's
            // exit-time chain replay, which surfaced it immediately (rt name mirrors the
            // effect name 1:1). Same body as GetConfirmedEgogift: nextBattleId (not
            // startBattleId) is the field this arm reads, and real static data never binds it
            // for these entries, so it correctly never clobbers the StartBattle_Abnormality
            // entry's nid that ran earlier in the same eventResultDataList loop.
            case "GetConfirmedEgogift":
            case "GetConfirmedEgogiftOnWinBattle":
            case "GetImmediateConfirmedEgogifts":
                if (rf.nextBattleId.HasValue) nid = rf.nextBattleId.Value;
                if (rf.itemReward is not null && rf.itemReward.rewardId.HasValue)
                    save.PushEgoGift(rf.itemReward.rewardId.Value);
                break;

            // switch(first) matches the token BEFORE the first '_' (see numbers/TryGetNumber
            // arms below) - "StartBattle_Abnormality" splits to first="StartBattle", so the
            // case label must be "StartBattle", not the full compound effect name. Root-cause
            // fix for a dead-code arm caught by the Fix-1 regression test (task-10 review):
            // the case label as originally written could never match, so this arm never fired
            // on real static data and nid stayed null/-1 despite the field wiring being correct.
            case "StartBattle":
                if (rf.startBattleId.HasValue) nid = rf.startBattleId.Value;
                break;

            case "MirrorDungeonAcquireCost":
                if (rf.itemReward is not null) save.AddCost(rf.itemReward.num);
                break;

            case "MirrorDungeonLossCost":
                if (rf.itemReward is not null) save.AddCost(-rf.itemReward.num);
                break;

            case "LoseHpMpDifferentAmount":
                if (target is null) return nid;
                if (!TryGetNumber(numbers, 0, out var hp)) return nid;
                if (!TryGetNumber(numbers, 1, out var mp)) return nid;
                ApplyHpSp(save, chosenPersonality, target, -hp, -mp);
                break;

            case "LoseHpMpSameAmount":
                if (target is null) return nid;
                if (!TryGetNumber(numbers, 0, out var lhma)) return nid;
                ApplyHpSp(save, chosenPersonality, target, -lhma, -lhma);
                break;

            case "LoseHpOnly":
                if (target is null) return nid;
                if (!TryGetNumber(numbers, 0, out var lho)) return nid;
                ApplyHpSp(save, chosenPersonality, target, -lho, 0);
                break;

            case "LoseRatioHpOnly":
                if (target is null) return nid;
                if (!TryGetNumber(numbers, 0, out var lr)) return nid;
                ApplyHpLossRatio(save, chosenPersonality, target, lr);
                break;

            case "LoseMpOnly":
                if (target is null) return nid;
                if (!TryGetNumber(numbers, 0, out var lmo)) return nid;
                ApplyHpSp(save, chosenPersonality, target, 0, -lmo);
                break;

            case "RecoverHpMpSameAmount":
                if (target is null) return nid;
                if (!TryGetNumber(numbers, 0, out var rhma)) return nid;
                ApplyHpSp(save, chosenPersonality, target, rhma, rhma);
                break;

            case "RecoverHpOnlyUntilMax":
                if (target is null) return nid;
                ApplyHpSp(save, chosenPersonality, target, 10000, 0);
                break;

            case "RecoverHpOnly":
                if (target is null) return nid;
                if (!TryGetNumber(numbers, 0, out var rho)) return nid;
                ApplyHpSp(save, chosenPersonality, target, rho, 0);
                break;

            case "RecoverMpOnlyUntilMax":
                if (target is null) return nid;
                ApplyHpSp(save, chosenPersonality, target, 0, 10000);
                break;

            case "RecoverMpOnly":
                if (target is null) return nid;
                if (!TryGetNumber(numbers, 0, out var rmo)) return nid;
                ApplyHpSp(save, chosenPersonality, target, 0, rmo);
                break;

            default:
                break; // unknown effect - no-op, matches Rust's `warn!` catch-all arm
        }

        return nid;
    }

    private static bool TryGetNumber(string[] numbers, int idx, out long value)
    {
        value = 0;
        return idx < numbers.Length && long.TryParse(numbers[idx], out value);
    }

    public static List<long> ApplyHpSpTargets(IDungeonEventSave save, long? chosenPersonality, string targetType)
    {
        switch (targetType)
        {
            case "EveryAlly":
                return save.GetUnitStats().Keys.ToList();

            case "ChosenPersonality":
                return chosenPersonality.HasValue ? new List<long> { chosenPersonality.Value } : new List<long>();

            case "RandomAlly":
                var keys = save.GetUnitStats().Keys.ToList();
                if (keys.Count == 0) return new List<long>();
                return new List<long> { keys[Random.Shared.Next(keys.Count)] };

            default:
                return new List<long>();
        }
    }

    public static void ApplyHpSp(IDungeonEventSave save, long? chosenPersonality, string target, long hpChange, long spChange)
    {
        var targets = ApplyHpSpTargets(save, chosenPersonality, target);
        var stats = save.GetUnitStats();
        foreach (var key in targets)
        {
            if (stats.TryGetValue(key, out var s))
            {
                s.hp += hpChange;
                s.sp += spChange;
            }
        }
        save.SetUnitStats(stats);
    }

    public static void ApplyHpLossRatio(IDungeonEventSave save, long? chosenPersonality, string target, long ratio)
    {
        var targets = ApplyHpSpTargets(save, chosenPersonality, target);
        var pct = ratio / 100f;
        var stats = save.GetUnitStats();
        foreach (var key in targets)
        {
            if (stats.TryGetValue(key, out var s))
            {
                s.hp -= (long)((float)s.hp * pct);
            }
        }
        save.SetUnitStats(stats);
    }
}
