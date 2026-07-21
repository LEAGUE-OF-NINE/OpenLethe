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

public static class MdEventManager
{
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
            foreach (var result in option.resultList)
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

        foreach (var dataList in er.eventResultDataList)
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
            case "GetConfirmedEgogift":
            case "GetImmediateConfirmedEgogifts":
                if (rf.nextBattleId.HasValue) nid = rf.nextBattleId.Value;
                if (rf.itemReward is not null && rf.itemReward.rewardId.HasValue)
                    save.PushEgoGift(rf.itemReward.rewardId.Value);
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
