using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.MirrorDungeon.Rules;
using OpenLethe.Server.Wire;

namespace OpenLethe.Tests;

// Component tests for the Task 3 domain primitives: StartRunRules (fresh run / mode-enter /
// constraints). NewRun_MatchesBuildFreshSave_ExceptStartDate used to hold the wire-typed
// handler helper (BuildFreshSave) as a reflection-based oracle proving NewRun was a
// byte-exact port; Task 12 deleted BuildFreshSave (dead once every caller migrated) along
// with that oracle test. NewRun's correctness is now covered by the 407-replay's
// EnterMirrorDungeon records (unchanged, byte-green) plus the tests below.
public class MdRulesStartRunTests
{
    [Fact]
    public void NewRun_NonDungeon7_UsesDefaultStartingCost()
    {
        var run = StartRunRules.NewRun(4, 0);
        Assert.Equal(500, run.Cost);
    }

    [Fact]
    public void EnterMode_IncrementsIdx()
    {
        var run = new Run { Idx = 2 };
        StartRunRules.EnterMode(run);
        Assert.Equal(3, run.Idx);
    }

    [Fact]
    public void AcquireConstraints_EmptySelection_RemovesEventAppendsEmptySelection()
    {
        var run = new Run();
        run.RewardEvents.Add(new RewardEvent { Rt = "GetConstraints", Pool = new List<long> { 101, 102, 103 } });
        run.Floor.Current.F = 2;

        StartRunRules.AcquireConstraints(run, new List<long>());

        Assert.DoesNotContain(run.RewardEvents, e => e.Rt == "GetConstraints");
        var sel = Assert.Single(run.ConstraintSelections);
        Assert.Equal(3, sel.Flooridx);
        Assert.Empty(sel.Ids);
    }

    [Fact]
    public void AcquireConstraints_WithSelection_DerivesIdsFromPoolByIndex()
    {
        var run = new Run();
        run.RewardEvents.Add(new RewardEvent { Rt = "GetConstraints", Pool = new List<long> { 101, 102, 103 } });
        run.RewardEvents.Add(new RewardEvent { Rt = "Other" });

        StartRunRules.AcquireConstraints(run, new List<long> { 0, 2 });

        var survivor = Assert.Single(run.RewardEvents); // GetConstraints removed, "Other" survives
        Assert.Equal("Other", survivor.Rt);
        var sel = Assert.Single(run.ConstraintSelections);
        Assert.Equal(new long[] { 101, 103 }, sel.Ids);
    }

    [Fact]
    public void AcquireConstraints_NoEventPresent_AppendsEmptySelectionWithoutThrowing()
    {
        var run = new Run();
        StartRunRules.AcquireConstraints(run, new List<long> { 0 });
        var sel = Assert.Single(run.ConstraintSelections);
        Assert.Empty(sel.Ids);
    }

    // ---- Task 4: start-buff / formation / detect economy ----

    [Fact]
    public void EnableStartBuff_ConvertedCost_ConvertsRemainderToScc_ZeroesStartBufPoint()
    {
        // Capture-verified run-1: startBufPoint 80, buffs [106,102] raw-cost 60 (== BasePoint),
        // remaining 20 -> scc += 20, cost = 200 + 20*PointToCostMultiplier(5) = 300.
        var run = new Run { Cost = 200, StartBuffPoint = 80 };

        var cost = StartRunRules.EnableStartBuff(run, new List<long> { 106, 102 }, enableConvertedCost: true);

        Assert.Equal(300, cost);
        Assert.Equal(300, run.Cost);
        Assert.Equal(20, run.Starlight.Scc);
        Assert.Equal(0, run.StartBuffPoint);
    }

    [Fact]
    public void EnableStartBuff_NotConverted_LeavesCostAndStartBufPointUntouched()
    {
        // Capture-verified run-2 shape: enableConvertedCost=false is a no-op beyond returning cost.
        var run = new Run { Cost = 200, StartBuffPoint = 120 };

        var cost = StartRunRules.EnableStartBuff(run, new List<long> { 102, 103, 106, 107 }, enableConvertedCost: false);

        Assert.Equal(200, cost);
        Assert.Equal(200, run.Cost);
        Assert.Equal(120, run.StartBuffPoint);
        Assert.Equal(0, run.Starlight.Scc);
    }

    [Fact]
    public void DetectStarlight_SetsDegidsAndIeedt_AppendsNewGiftsOnly()
    {
        var run = new Run();
        run.Gifts.Items.Add(new EgoGift { Id = 9001 });
        run.Starlight.Ieedt = 1;

        StartRunRules.DetectStarlight(run, new List<long> { 9001, 9002 });

        Assert.Equal(new long[] { 9001, 9002 }, run.Starlight.Degids);
        Assert.Equal(0, run.Starlight.Ieedt);
        Assert.Equal(new long[] { 9001, 9002 }, run.Gifts.Items.Select(g => g.Id));
    }

    [Fact]
    public void SelectFormation_SetsSpidAndStartBufPoint_EnumeratesEgoSlots()
    {
        var run = new Run();
        var formation = new List<Formation>
        {
            new() { nextPersonalityId = 1001, egos = new List<Egos2> { new() { nextEgoId = 5001 } } },
            new() { nextPersonalityId = 1002, egos = new List<Egos2>() },
        };
        var gradeMap = new Dictionary<long, long> { [1001] = 2 };
        var levelMap = new Dictionary<long, long> { [1001] = 45 };

        StartRunRules.SelectFormation(run, formation, gradeMap, levelMap);

        Assert.Equal(new long[] { 1001, 1002 }, run.Spid);
        Assert.Equal(2, run.Party.Count);
        Assert.Equal(2, run.Party[0].Gacksung); // looked up
        Assert.Equal(45, run.Party[0].Level);   // looked up
        Assert.Equal(4, run.Party[1].Gacksung); // default fallback
        Assert.Equal(60, run.Party[1].Level);   // default fallback
        Assert.Equal(10000, run.Party[0].CurrentHp);
        Assert.Single(run.Party[0].EgoSkills);
        Assert.Equal(5001, run.Party[0].EgoSkills[0].Id);
        // basePoint(60) + detectThemeFloorDefaultPoint(20) - capture-verified 80.
        Assert.Equal(80, run.StartBuffPoint);
        Assert.Equal(20, run.Starlight.Pfb);
    }

    [Fact]
    public void PurchaseFormation_ChargesUsedCost_ReplacesMatchedUnitsOnly()
    {
        var run = new Run { Cost = 500 };
        run.Party.Add(new PartyUnit { PersonalityId = 1001, EgoSkills = new List<EgoSkill> { new() { Id = 5001 } } });
        run.Party.Add(new PartyUnit { PersonalityId = 1002 });
        var formation = new List<Formation>
        {
            new()
            {
                pervPersonalityId = 1001,
                nextPersonalityId = 1003,
                egos = new List<Egos2> { new() { prevEgoId = 5001, nextEgoId = 5002 } },
            },
        };

        StartRunRules.PurchaseFormation(run, formation);

        Assert.Equal(400, run.Cost);
        Assert.Equal(1003, run.Party[0].PersonalityId);
        Assert.Equal(5002, run.Party[0].EgoSkills[0].Id);
        Assert.Equal(1002, run.Party[1].PersonalityId); // unmatched unit untouched
    }

    [Fact]
    public void AcquireStartAndCreateThemePool_SetsKeywordSepsCreatedAndPerUnitMlos()
    {
        var run = new Run();
        run.StartPools.Add(new StartEgoGiftPool { SetId = 0, Keyword = "Combustion", Pool = new() { 9001 } });
        run.Party.Add(new PartyUnit());
        run.Party.Add(new PartyUnit());
        var sepsCreatedBefore = run.SepsCreated;

        StartRunRules.AcquireStartAndCreateThemePool(run, selectedSetId: 0, new List<long> { 9001 }, detectToggle: true);

        Assert.Equal(sepsCreatedBefore + 1, run.SepsCreated);
        Assert.Equal("Combustion", run.StartKeyword);
        Assert.Equal(0, run.SepsId);
        Assert.Empty(run.StartPools);
        Assert.Equal(1, run.Starlight.Ieedt);
        Assert.Contains(run.Gifts.Items, g => g.Id == 9001);
        Assert.Equal(3, run.LevelOffsets.Sbmlos); // buffs [106,102] ENTER_1ST_FLOOR - capture-verified 3
        Assert.All(run.Party, u => Assert.Equal(run.LevelOffsets.Sbmlos + run.LevelOffsets.Egmlos, u.Mlos));
        Assert.Equal(8, run.ThemePools.Count); // RNG-masked contents; only the count is guaranteed
    }
}
