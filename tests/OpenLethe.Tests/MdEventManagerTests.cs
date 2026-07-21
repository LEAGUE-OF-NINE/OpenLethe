using System.Collections.Generic;
using OpenLethe.Server;
using OpenLethe.Server.Wire;
using Xunit;

// RNG (RandomAlly) uses Random.Shared - assert structural invariants, never exact picks.
// See memory: mirror-dungeon-rng-is-nondeterministic.
public class MdEventManagerTests
{
    private static MirrorOriginSaveInfo NewSave(long pid = 7, long hp = 10000, long sp = 5000)
    {
        var save = new MirrorOriginSaveInfo();
        save.currentInfo.dul.Add(new Dungeonunitlist1 { pid = pid, ch = hp, cm = sp });
        return save;
    }

    // ---- MdEventData loader ----

    [Fact]
    public void MdEventData_LoadsRealEvents_AndGetByIdWorks()
    {
        Assert.True(MdEventData.Count > 0);
        // static-data/abnormality-event/mirror-dungeon-action-choice-event.json id 900011
        var ev = MdEventData.GetById(900011);
        Assert.NotNull(ev);
        Assert.NotNull(ev!.actionEvent);
    }

    [Fact]
    public void MdEventData_UnknownId_ReturnsNull()
    {
        Assert.Null(MdEventData.GetById(999999999));
    }

    // ---- ProcessEvent / action-event nextEventID propagation ----

    [Fact]
    public void UpdateActionEventRewards_ReturnsNextEventIDFromChosenOption()
    {
        var ev = new ActionEvent
        {
            eachOptionList = new List<EachOption>
            {
                new() { resultList = new List<ActionResult> { new() { nextEventID = 800101 } } },
                new() { resultList = new List<ActionResult> { new() { nextEventID = 800202 } } },
            },
        };
        var save = new MdEventSave(NewSave());

        var nid = MdEventManager.UpdateActionEventRewards(ev, 1, save);

        Assert.Equal(800202, nid);
    }

    [Fact]
    public void UpdateActionEventRewards_ChoiceIdxOutOfRange_ReturnsMinusOne()
    {
        var ev = new ActionEvent
        {
            eachOptionList = new List<EachOption> { new() { resultList = new List<ActionResult> { new() { nextEventID = 1 } } } },
        };
        var save = new MdEventSave(NewSave());

        Assert.Equal(-1, MdEventManager.UpdateActionEventRewards(ev, 5, save));
    }

    // ---- ApplyResultForm effect parsing ----

    [Fact]
    public void ApplyResultForm_LoseHpOnly_EveryAlly_ReducesHp()
    {
        var save = new MdEventSave(NewSave(hp: 10000));
        var rf = new ResultForm { resultEffectTarget = "EveryAlly", resultEffect = "LoseHpOnly_100" };

        MdEventManager.ApplyResultForm(rf, null, save);

        Assert.Equal(9900, save.GetUnitStats()[7].hp);
    }

    [Fact]
    public void ApplyResultForm_RecoverHpOnlyUntilMax_AddsTenThousand()
    {
        var save = new MdEventSave(NewSave(hp: 10000));
        var rf = new ResultForm { resultEffectTarget = "EveryAlly", resultEffect = "RecoverHpOnlyUntilMax" };

        MdEventManager.ApplyResultForm(rf, null, save);

        Assert.Equal(20000, save.GetUnitStats()[7].hp);
    }

    [Fact]
    public void ApplyResultForm_LoseHpMpDifferentAmount_AppliesBoth()
    {
        var save = new MdEventSave(NewSave(hp: 10000, sp: 5000));
        var rf = new ResultForm { resultEffectTarget = "EveryAlly", resultEffect = "LoseHpMpDifferentAmount_100_50" };

        MdEventManager.ApplyResultForm(rf, null, save);

        var stats = save.GetUnitStats()[7];
        Assert.Equal(9900, stats.hp);
        Assert.Equal(4950, stats.sp);
    }

    [Fact]
    public void ApplyResultForm_LoseRatioHpOnly_TenPercent()
    {
        var save = new MdEventSave(NewSave(hp: 10000));
        var rf = new ResultForm { resultEffectTarget = "EveryAlly", resultEffect = "LoseRatioHpOnly_10" };

        MdEventManager.ApplyResultForm(rf, null, save);

        Assert.Equal(9000, save.GetUnitStats()[7].hp);
    }

    [Fact]
    public void ApplyResultForm_MirrorDungeonAcquireCost_AddsCost()
    {
        var raw = NewSave();
        var save = new MdEventSave(raw);
        var rf = new ResultForm { resultEffect = "MirrorDungeonAcquireCost", itemReward = new ItemReward { num = 200 } };

        MdEventManager.ApplyResultForm(rf, null, save);

        Assert.Equal(200, raw.currentInfo.cost);
    }

    [Fact]
    public void ApplyResultForm_MirrorDungeonLossCost_SubtractsCost()
    {
        var raw = NewSave();
        var save = new MdEventSave(raw);
        var rf = new ResultForm { resultEffect = "MirrorDungeonLossCost", itemReward = new ItemReward { num = 100 } };

        MdEventManager.ApplyResultForm(rf, null, save);

        Assert.Equal(-100, raw.currentInfo.cost);
    }

    [Fact]
    public void ApplyResultForm_GetConfirmedEgogift_PushesEgoAndReturnsNextBattleId()
    {
        var save = new MdEventSave(NewSave());
        var rf = new ResultForm
        {
            resultEffect = "GetConfirmedEgogift",
            itemReward = new ItemReward { rewardId = 9001 },
            nextBattleId = 555,
        };

        var nid = MdEventManager.ApplyResultForm(rf, null, save);

        Assert.Equal(555, nid);
    }

    [Fact]
    public void ApplyResultForm_MissingTarget_DoesNothingAndReturnsNull()
    {
        var save = new MdEventSave(NewSave(hp: 10000));
        var rf = new ResultForm { resultEffectTarget = null, resultEffect = "LoseHpOnly_100" };

        var nid = MdEventManager.ApplyResultForm(rf, null, save);

        Assert.Null(nid);
        Assert.Equal(10000, save.GetUnitStats()[7].hp);
    }

    // ---- targeting ----

    [Fact]
    public void ApplyHpSp_ChosenPersonality_OnlyAffectsThatPid()
    {
        var save = NewSave();
        save.currentInfo.dul.Add(new Dungeonunitlist1 { pid = 8, ch = 10000, cm = 5000 });
        var wrapped = new MdEventSave(save);

        MdEventManager.ApplyHpSp(wrapped, 7, "ChosenPersonality", -100, 0);

        Assert.Equal(9900, wrapped.GetUnitStats()[7].hp);
        Assert.Equal(10000, wrapped.GetUnitStats()[8].hp);
    }

    [Fact]
    public void ApplyHpSp_RandomAlly_PicksAValidPid()
    {
        var save = new MdEventSave(NewSave(pid: 7, hp: 10000));

        MdEventManager.ApplyHpSp(save, null, "RandomAlly", -100, 0);

        Assert.Equal(9900, save.GetUnitStats()[7].hp);
    }

    [Fact]
    public void ApplyHpSpTargets_ChosenPersonality_NoneSupplied_ReturnsEmpty()
    {
        var save = new MdEventSave(NewSave());
        Assert.Empty(MdEventManager.ApplyHpSpTargets(save, null, "ChosenPersonality"));
    }

    // ---- personality event path ----

    [Fact]
    public void UpdatePersonalityEventReward_CoinStateIndexesEventResults_AndAppliesEffect()
    {
        var save = new MdEventSave(NewSave(hp: 10000));
        var pe = new PersonalityEvent
        {
            eventResults = new List<PersonalityEventResult>
            {
                new() { eventResultDataList = new List<PersonalityResultData> { new() { nextEventID = 1 } } },
                new() { eventResultDataList = new List<PersonalityResultData> { new() { nextEventID = 2 } } },
                new()
                {
                    eventResultDataList = new List<PersonalityResultData>
                    {
                        new()
                        {
                            nextEventID = 42,
                            eventResultDataList = new List<ResultFormWrapper>
                            {
                                new() { resultForm = new ResultForm { resultEffectTarget = "EveryAlly", resultEffect = "LoseHpOnly_100" } },
                            },
                        },
                    },
                },
            },
        };

        var nid = MdEventManager.UpdatePersonalityEventReward(pe, chosenPersonality: 0, coinState: 2, save);

        Assert.Equal(42, nid);
        Assert.Equal(9900, save.GetUnitStats()[7].hp);
    }

    [Fact]
    public void UpdatePersonalityEventReward_CoinStateOutOfRange_ReturnsMinusOne()
    {
        var pe = new PersonalityEvent { eventResults = new List<PersonalityEventResult>() };
        var save = new MdEventSave(NewSave());

        Assert.Equal(-1, MdEventManager.UpdatePersonalityEventReward(pe, 0, 5, save));
    }
}
