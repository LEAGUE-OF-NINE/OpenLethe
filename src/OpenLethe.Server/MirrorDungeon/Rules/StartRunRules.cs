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
    /// <summary>
    /// The current season's Mirror Dungeon id. All four entries of the client's mirrordungeon
    /// static table (typeIndex 0..3) are id 8; the entrance's "DUNGEON NN" reads this value, and
    /// anything else (0, or last season's 7) misses the static data - the plate shows 00 and the
    /// banner falls back to the theme-floor table's Korean desc.
    /// </summary>
    public const long CurrentDungeonId = 8;

    /// Normalizes a dungeonId from any source to the current season (Enter: every new run is one).
    public static long NormalizeDungeonIdValue(long dungeonId) =>
        dungeonId == CurrentDungeonId ? dungeonId : CurrentDungeonId;

    /// ReEnter: moves a stored run of an older season onto the current one. A save with no run
    /// (dungeonId <= 0, the -1 "no dungeon" marker) is left alone - reporting it as dungeon 8 made
    /// the client believe a run was still open after a forfeit had already ended it.
    public static void NormalizeDungeonId(MirrorOriginSaveInfo save)
    {
        if (save is not null && save.dungeonId > 0) save.dungeonId = NormalizeDungeonIdValue(save.dungeonId);
    }

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
    // The client drives its mode off this counter's VALUE, not off a delta: EnterMD=0, first
    // SelectThemeFloor=1, InfiniteMode=2, ExtremeMode=3 (see the EnterInfiniteMode/EnterExtremeMode
    // handler notes). Forcing the exact value matters at an act boundary: the client calls
    // EnterInfiniteMode mid-run (floor 5, 10, ...) and a drifting +1 made it treat the run as
    // finished and settle on the spot.
    public static void EnterMode(Run run, long mode) => run.Idx = mode;

    // /api/AcquireMirrorDungeonConstraints: the Adversity pick for the floor about to start
    // (cn.f + 1 - the client posts while still on the boss node it just cleared). selectIdxList
    // holds positions in the offered list, not constraint ids. Retail (md-extreme-run seq298,
    // run-2 seq332/375/404/432, all empty picks) consumes the GetConstraints popup and appends one
    // scinfos entry; cfs is NOT touched here (the floor-clear writes it).
    // Each picked id is also granted as an ego gift: 995xxx are hidden CONSTRAINT gifts
    // (hidden-ego-gift-mirrordungeon), the same way the 992xxx HARDSHIP enemy buffs land in egs
    // at AcquireRewardEgoGiftsWithEnemyBuf. The client applies an Adversity's effect from the gift
    // it owns, so a pick recorded only in scinfos did nothing in game.
    // The client can post the same pick more than once. The popup is gone after the first call,
    // so a repeat resolves its indices against the floor's list rebuilt from the table, and it
    // replaces this floor's pick instead of appending an empty one over it.
    public static void AcquireConstraints(Run run, List<long> selectIdxList)
    {
        var floor = run.Floor.Current.F + 1;
        var ev = run.RewardEvents.FirstOrDefault(e => e.Rt == "GetConstraints");
        if (ev is not null) run.RewardEvents.Remove(ev);

        // Undo an earlier pick for this floor before applying this one.
        var earlier = run.ConstraintSelections.Where(s => s.Flooridx == floor).SelectMany(s => s.Ids).ToHashSet();
        run.ConstraintSelections.RemoveAll(s => s.Flooridx == floor);
        var keptElsewhere = run.ConstraintSelections.SelectMany(s => s.Ids).ToHashSet();
        run.Gifts.Items.RemoveAll(g => earlier.Contains(g.Id) && !keptElsewhere.Contains(g.Id));

        var pool = ev?.Pool ?? OpenLethe.Server.MdConstraints.ForFloor(floor, keptElsewhere);
        // Out-of-range indices are dropped instead of throwing, so a bogus index cannot 500 the
        // endpoint (same defensive stance AcquireBattleReward takes).
        var ids = selectIdxList
            .Where(i => i >= 0 && i < pool.Count)
            .Select(i => pool[(int)i])
            .Distinct()
            .ToList();
        run.ConstraintSelections.Add(new ConstraintSelection { Flooridx = floor, Ids = ids });

        foreach (var id in ids)
            if (!run.Gifts.Items.Any(g => g.Id == id))
                run.Gifts.Items.Add(new EgoGift { Id = id });
    }

    // /api/DetectMirrorDungeonThemeFloorByStarlight: swap the offered pack originThemeFloorId for
    // detectThemeFloorId in place (Idx and position kept, so the pool stays at four), paid with
    // detectThemeFloorDefaultPoint (20) starlight out of the run's banked tally (scc, the same one
    // EnableStartBuff feeds and AcquireExitReward settles into pslp). If origin is not in the pool
    // (not sent, or already swapped) the pack is appended instead so the button never no-ops.
    // The swap is never refused for lack of points - a deliberate deviation from vanilla's
    // affordability check, since the capture never exercises the refused branch.
    // customThemes: the account's uploaded custom packs (Account.CustomTheme). The client offers
    // those as detection targets too; without them the lookup below missed every custom pack and
    // the detect silently did nothing.
    public static long DetectThemeFloor(Run run, long originThemeFloorId, long detectThemeFloorId,
        IReadOnlyList<ThemeStatic>? customThemes = null)
    {
        if (detectThemeFloorId == 0) return 0;
        if (run.ThemePools.Any(t => t.Tfid == detectThemeFloorId)) return 0;

        var themePool = new MdThemePool(customThemes); // namespace OpenLethe.Server.MirrorDungeon.Data (using above)
        if (!themePool.pools.ContainsKey(detectThemeFloorId)) return 0;

        // The floor being selected == the number of floors the run has built so far.
        var floor = run.ThemeFloors.Count > 0 ? run.ThemeFloors.Count : run.Floor.Current.F + 1;
        var slot = originThemeFloorId != 0
            ? run.ThemePools.FirstOrDefault(t => t.Tfid == originThemeFloorId)
            : null;

        if (slot is not null)
        {
            slot.Tfid = detectThemeFloorId;
            slot.Egs = themePool.SelectRandomEgosFromPool(detectThemeFloorId, 4, floor);
            slot.Upegs = new List<long> { 9051, 9048, 9001, 9066 };
        }
        else
        {
            run.ThemePools.Add(new ThemePool
            {
                // Same difficulty tier as the pool it joins (MapGenerator.NextFloorPools).
                Idx = MapGenerator.IsNormalMode(run) ? 0 : floor / 5 + 1,
                Tfid = detectThemeFloorId,
                Egs = themePool.SelectRandomEgosFromPool(detectThemeFloorId, 4, floor),
                Upegs = new List<long> { 9051, 9048, 9001, 9066 },
            });
        }

        var paid = Math.Min(20, run.Starlight.Scc);
        run.Starlight.Scc -= paid;
        return paid;
    }

    // Port of the EnableStartBuffMirrorDungeon handler body (Handlers/MirrorDungeon.cs :99-140).
    // Spends the run's start-buff points; the run's `cost` budget starts at StartingCost and
    // grows as leftover start-buff points convert into currency. Returns cost for the DTO.
    // ponytail: enableConvertedCost==false is unexercised by the capture - it's a pure no-op
    // beyond returning the unchanged cost, same as the handler it replaces.
    public static long EnableStartBuff(Run run, List<long> buffids, bool enableConvertedCost)
    {
        // Persist the picks on the run: the later steps (level-cap offsets at AcquireStart, per new
        // theme and on battle win; ADDITIONAL_START_COST) derive from them instead of the capture's
        // hardcoded pair. Stored before the point math so every branch keeps them.
        run.StartBuffIds = buffids.Distinct().OrderBy(id => id).ToList();
        run.StartBuffsChosen = true;

        var cost = run.Cost;
        if (enableConvertedCost)
        {
            // Capture-verified (the run's only branch): the SelectFormation bonus points
            // (startBufPoint minus the buffs' raw point cost) convert - the raw remainder into
            // starlight (scc) and its *multiplier into the cost budget - and startBufPoint
            // zeroes out. An overspend (negative remainder) still comes out of starlight, but
            // never out of the cost budget: the conversion only ever adds cost.
            var remaining = run.StartBuffPoint - MdStartBuffs.RawSpend(buffids);
            run.Starlight.Scc += remaining;
            cost += Math.Max(0, remaining) * MdStartBuffs.PointToCostMultiplier;
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

    // PurchaseFormationMirrorDungeon: a swap of matched units' personality/ego ids (unmatched
    // units left untouched) for shop.changeFormation.price (100) per use. How many uses are
    // allowed is table-driven too: 3 on the pre-run formation screen
    // (shop.changeFormation.changeableCount) and shopInfoByType.changeFormationCount (1) inside a
    // shop visit. Uses are counted in the shop's `cf`, which MapNodeRules clears with the rest of
    // the shop state on every shop entry. Returns false when the allowance is used up - the
    // caller then answers with the unchanged save.
    public static bool PurchaseFormation(Run run, List<Formation> formation)
    {
        var superShop = SharedRules.IsSuperShop(run);
        var cap = superShop is { } super
            ? MdShopTable.ShopChangeFormationCount(super)
            : MdShopTable.ChangeFormationCount;
        if (run.Shop.Cf >= cap) return false;

        var usedCost = Effects.ApplyRaisedMarketPrice(run, MdShopTable.ChangeFormationPrice);
        var replaceMap = new Dictionary<long, Formation>();
        foreach (var f in formation) replaceMap[f.pervPersonalityId] = f; // last wins, matches Rust HashMap collect

        run.Cost -= usedCost;
        run.Shop.Cf += 1;
        foreach (var unit in run.Party)
        {
            if (!replaceMap.TryGetValue(unit.PersonalityId, out var replacement)) continue;
            var egoMap = new Dictionary<long, long>();
            foreach (var e in replacement.egos) egoMap[e.prevEgoId] = e.nextEgoId; // last wins
            unit.PersonalityId = replacement.nextPersonalityId;
            foreach (var ego in unit.EgoSkills)
                if (egoMap.TryGetValue(ego.Id, out var newId)) ego.Id = newId;
        }

        return true;
    }

    // Port of the AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon handler body
    // (Handlers/MirrorDungeonMap.cs :186-208). The theme-pool contents are RNG (masked in
    // replay); MapGenerator doesn't exist yet (Task 5), so this keeps calling the existing
    // MdMapGen.PickThemes and translates its wire output into run.ThemePools.
    // customThemes: the account's uploaded packs, which the run-start offer can include.
    public static void AcquireStartAndCreateThemePool(Run run, long selectedSetId, List<long> selectedEgoGiftIds, bool detectToggle,
        IReadOnlyList<ThemeStatic>? customThemes = null)
    {
        run.Gifts.Items.AddRange(selectedEgoGiftIds.Select(id => new EgoGift { Id = id }));
        // sepsId records which starter set was chosen (= the request's selectedSetId).
        run.SepsId = selectedSetId;
        // The enabled buffs' own ADDITIONAL_START_COST (Star of the Beginning +150/+250/+250,
        // Star-shower +400/+500/+700) lands on the run's `cost` budget here. The persisted picks are
        // authoritative; the extra-select count is only the replay fallback for a run that never
        // stored them (both captured runs' golden depends on that reconstruction).
        run.Cost += run.StartBuffsChosen || run.StartBuffIds.Count > 0
            ? MdStartBuffs.StartCostOf(run.StartBuffIds)
            : MdStartBuffs.AdditionalStartCost(selectedEgoGiftIds.Count - 1);
        run.ThemePools = OpenLethe.Server.MdMapGen.PickThemes(SharedRules.CurrentFloor(run), null, StartBuffEffects.ThemePoolSize(run), customThemes)
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
        run.LevelOffsets.Sbmlos = StartBuffEffects.LevelOffsets(run).EnterFirstFloor;
        var startLevelCap = run.LevelOffsets.Sbmlos + run.LevelOffsets.Egmlos;
        foreach (var u in run.Party) u.Mlos = startLevelCap;

        // Perfected Possibility's "upon entry" Vestiges and Keyword pick popup - after startKeyword
        // is set, which the picks draw from. (Moon Star-shop's floor-1 gifts come with floor 1.)
        StartBuffEffects.OnEnterFirstFloor(run);
    }
}
