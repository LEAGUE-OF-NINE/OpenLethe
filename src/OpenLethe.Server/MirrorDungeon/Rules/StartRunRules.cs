using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.MirrorDungeon.Rules;

// Start-of-run rules: fresh run creation, the mode-enter counter, floor-boundary constraint
// selection, and the start-buff/formation/detect economy. Verbatim ports of
// Handlers/MirrorDungeon.cs BuildFreshSave/EnableStartBuff/DetectStarlight, the idx+=1 handlers,
// AcquireMirrorDungeonConstraints, Handlers/MirrorDungeonShop.cs SelectFormation/
// PurchaseFormation, and Handlers/MirrorDungeonMap.cs AcquireStartEgoGiftsAndCreateThemePool.
// See the handler-migration Task 3/4 briefs.
public static class StartRunRules
{
    // The MD7 start buffs that grant personality level-cap offsets, read from static data.
    // Verbatim port of MirrorDungeonMap.cs LevelOffsets (:30) - both captured runs enable
    // exactly these two level-granting buffs, so the offset VALUES are the same for both.
    private static readonly MdLevelOffsets LevelOffsets = MdStartBuffs.LevelOffsets(new long[] { 106, 102 });

    // Verbatim port of Handlers/MirrorDungeon.cs BuildFreshSave (:193-246), built directly as a
    // Run instead of the wire MirrorOriginSaveInfo. Fields left unset take Run's own defaults,
    // which mirror the wire type's field initializers exactly (e.g. Starlight.Pslp=383,
    // Missions.Sec=-1/Nlm=1, LevelOffsets all zero, Shop empty) - see WireMapper/Model defaults.
    public static Run NewRun(long dungeonId, long idx)
    {
        var seps = new List<StartEgoGiftPool>
        {
            new() { SetId = 0, Keyword = "Combustion", Pool = new() { 9001, 9009, 9103 } },
            new() { SetId = 1, Keyword = "Laceration", Pool = new() { 9005, 9029, 9108 } },
            new() { SetId = 2, Keyword = "Vibration", Pool = new() { 9044, 9086, 9113 } },
            new() { SetId = 3, Keyword = "Burst", Pool = new() { 9047, 9093, 9117 } },
            new() { SetId = 4, Keyword = "Sinking", Pool = new() { 9041, 9054, 9124 } },
            new() { SetId = 5, Keyword = "Breath", Pool = new() { 9046, 9051, 9129 } },
            new() { SetId = 6, Keyword = "Charge", Pool = new() { 9043, 9052, 9134 } },
            new() { SetId = 7, Keyword = "Slash", Pool = new() { 9032, 9194, 9140 } },
            new() { SetId = 8, Keyword = "Penetrate", Pool = new() { 9030, 9198, 9145 } },
            new() { SetId = 9, Keyword = "Hit", Pool = new() { 9012, 9202, 9150 } },
        };
        var stocks = new List<SkillStock>
        {
            new() { T = "CR", N = 0 }, new() { T = "SC", N = 0 }, new() { T = "AM", N = 0 },
            new() { T = "SH", N = 0 }, new() { T = "AZ", N = 0 }, new() { T = "IN", N = 0 },
            new() { T = "VI", N = 0 },
        };
        return new Run
        {
            DungeonId = dungeonId,
            Idx = idx,
            Eid = -1,
            SepsId = 0,
            StartPools = seps,
            SepsCreated = 1,
            Ri = 1,
            Cost = Effects.StartingCost(dungeonId),
            UsedCost = 0,
            StartKeyword = "None",
            StartBuffPoint = 0,
            ConstraintScores = new List<ConstraintScore> { new() { Floor = -1, Difficulty = 0 } },
            Nr = 0,
            SkillStocks = stocks,
            Dn = 0,
            EncounterStatistics = new List<long> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            IsEndDungeon = 0,
            IsReset = 0,
            Version = 2,
            StartDate = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    // The run's mode-enter counter, shared by EnterExtremeMode/EnterInfiniteMode
    // (EnterMD=0, first SelectThemeFloor=1, InfiniteMode=2, ExtremeMode=3).
    public static void EnterMode(Run run) => run.Idx += 1;

    // Port of the AcquireMirrorDungeonConstraints handler body (Handlers/MirrorDungeon.cs
    // :172-177): consume the GetConstraints reward event, append a ConstraintSelection for the
    // floor about to start.
    public static void AcquireConstraints(Run run, List<long> selectIdxList)
    {
        var ev = run.RewardEvents.FirstOrDefault(e => e.Rt == "GetConstraints");
        if (ev is not null) run.RewardEvents.Remove(ev);
        // ponytail: non-empty selectIdxList is unexercised by the capture - derive ids from the
        // consumed GetConstraints pool by index, same shape the client sends selections in.
        var ids = ev is not null ? selectIdxList.Select(i => ev.Pool[(int)i]).ToList() : new List<long>();
        run.ConstraintSelections.Add(new ConstraintSelection { Flooridx = run.Floor.Current.F + 1, Ids = ids });
    }

    // Port of the EnableStartBuffMirrorDungeon handler body (Handlers/MirrorDungeon.cs :99-140).
    // Spends the run's start-buff points; the run's `cost` budget starts at StartingCost and
    // grows as leftover start-buff points convert into currency. Returns cost for the DTO.
    // ponytail: enableConvertedCost==false is unexercised by the capture - it's a pure no-op
    // beyond returning the unchanged cost, same as the handler it replaces.
    public static long EnableStartBuff(Run run, List<long> buffids, bool enableConvertedCost)
    {
        var cost = run.Cost;
        if (enableConvertedCost)
        {
            // Capture-verified (the run's only branch): the SelectFormation bonus points
            // (startBufPoint minus the buffs' raw point cost) convert - the raw remainder into
            // starlight (scc) and its *multiplier into the cost budget - and startBufPoint
            // zeroes out.
            var remaining = run.StartBuffPoint - MdStartBuffs.RawSpend(buffids);
            run.Starlight.Scc += remaining;
            cost += remaining * MdStartBuffs.PointToCostMultiplier;
            run.Cost = cost;
            run.StartBuffPoint = 0;
        }
        return cost;
    }

    // Port of the DetectMirrorDungeonEgogiftByStarlight handler body (Handlers/MirrorDungeon.cs
    // :153-157): record the detected picks, reset the detect-toggle, grant any not-yet-owned gift.
    public static void DetectStarlight(Run run, List<long> egogiftIds)
    {
        run.Starlight.Degids = egogiftIds;
        run.Starlight.Ieedt = 0;
        foreach (var id in egogiftIds)
            if (!run.Gifts.Items.Any(g => g.Id == id))
                run.Gifts.Items.Add(new EgoGift { Id = id });
    }

    // Port of the SelectFormationMirrorDungeon handler body (Handlers/MirrorDungeonShop.cs
    // :412-437). gradeMap/levelMap are the account's per-identity gacksung/level, derived by the
    // handler from AccountDefaults.DerivePersonalities (account state, not part of the run).
    public static void SelectFormation(Run run, List<Formation> formation, Dictionary<long, long> gradeMap, Dictionary<long, long> levelMap)
    {
        run.Party = formation.Select(u => new PartyUnit
        {
            UpgradeIndices = new(),
            Mlos = 0,
            PersonalityId = u.nextPersonalityId,
            CurrentHp = 10000,
            Cm = 0,
            Mhos = 0,
            Gacksung = gradeMap.TryGetValue(u.nextPersonalityId, out var gr) ? gr : 4,
            Level = levelMap.TryGetValue(u.nextPersonalityId, out var lv) ? lv : 60,
            EgoSkills = u.egos.Select(e => new EgoSkill { Id = e.nextEgoId, G = 0, Idx = OpenLethe.Server.MdEgoGrades.SlotFor(e.nextEgoId) }).ToList(),
            Isp = 0,
        }).ToList();
        run.Spid = formation.Select(u => u.nextPersonalityId).ToList();
        // basePoint (60, static) + the SelectFormation bonus (detectThemeFloorDefaultPoint,
        // 20) - capture-verified 80 - also lands in Starlight.Pfb for EnableStartBuff to spend.
        run.StartBuffPoint = MdStartBuffs.BasePoint + OpenLethe.Server.MdEgoData.DetectThemeFloorDefaultPoint;
        run.Starlight.Pfb = OpenLethe.Server.MdEgoData.DetectThemeFloorDefaultPoint;
    }

    // Port of the PurchaseFormationMirrorDungeon handler body (Handlers/MirrorDungeonShop.cs
    // :453-466): a flat 100-cost swap of matched units' personality/ego ids, unmatched units left
    // untouched.
    public static void PurchaseFormation(Run run, List<Formation> formation)
    {
        const long usedCost = 100;
        var replaceMap = new Dictionary<long, Formation>();
        foreach (var f in formation) replaceMap[f.pervPersonalityId] = f; // last wins, matches Rust HashMap collect

        run.Cost -= usedCost;
        foreach (var unit in run.Party)
        {
            if (!replaceMap.TryGetValue(unit.PersonalityId, out var replacement)) continue;
            var egoMap = new Dictionary<long, long>();
            foreach (var e in replacement.egos) egoMap[e.prevEgoId] = e.nextEgoId; // last wins
            unit.PersonalityId = replacement.nextPersonalityId;
            foreach (var ego in unit.EgoSkills)
                if (egoMap.TryGetValue(ego.Id, out var newId)) ego.Id = newId;
        }
    }

    // Port of the AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon handler body
    // (Handlers/MirrorDungeonMap.cs :186-208). The theme-pool contents are RNG (masked in
    // replay); MapGenerator doesn't exist yet (Task 5), so this keeps calling the existing
    // MdMapGen.PickThemes and translates its wire output into run.ThemePools.
    public static void AcquireStartAndCreateThemePool(Run run, long selectedSetId, List<long> selectedEgoGiftIds, bool detectToggle)
    {
        run.Gifts.Items.AddRange(selectedEgoGiftIds.Select(id => new EgoGift { Id = id }));
        // sepsId records which starter set was chosen (= the request's selectedSetId).
        run.SepsId = selectedSetId;
        // ADDITIONAL_START_COST from the enabled start buffs lands on the run's `cost` budget
        // here - the buffs aren't persisted, but the count of extra ego-gift selections reveals
        // them (buff103: +1 select => +400).
        run.Cost += MdStartBuffs.AdditionalStartCost(selectedEgoGiftIds.Count - 1);
        run.ThemePools = OpenLethe.Server.MdMapGen.PickThemes(SharedRules.CurrentFloor(run))
            .Select(t => new ThemePool { Idx = t.idx, Tfid = t.tfid, Egs = t.egs, Upegs = t.upegs })
            .ToList();
        // startKeyword is fixed by the chosen starter theme-set's keyword - look it up in the
        // still-intact StartPools catalog BEFORE clearing it below.
        var chosenKeyword = run.StartPools.FirstOrDefault(s => s.SetId == selectedSetId)?.Keyword;
        if (chosenKeyword is not null) run.StartKeyword = chosenKeyword;
        run.StartPools = new();
        run.SepsCreated += 1;
        run.Starlight.Ieedt = detectToggle ? 1 : 0;
        // Floor-1 personality level cap = the enabled start buffs' ENTER_1ST_FLOOR offset.
        run.LevelOffsets.Sbmlos = LevelOffsets.EnterFirstFloor;
        var startLevelCap = run.LevelOffsets.Sbmlos + run.LevelOffsets.Egmlos;
        foreach (var u in run.Party) u.Mlos = startLevelCap;
    }
}
