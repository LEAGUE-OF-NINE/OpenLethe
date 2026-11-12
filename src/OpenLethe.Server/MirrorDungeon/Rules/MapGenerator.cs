using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Mapping;
using OpenLethe.Server.MirrorDungeon.Model;

namespace OpenLethe.Server.MirrorDungeon.Rules;

// Floor generation + theme-pool re-roll. Verbatim ports of the RecreateThemeFloorPoolMirrorDungeon
// (Handlers/MirrorDungeonMap.cs :198-225) and SelectThemeFloorMirrorDungeon (:227-296) handler
// bodies. `OpenLethe.Server.MdMapGen` (map_gen.rs port) is MD-exclusive but wire-shaped - its
// GenerateNewFloor/UpdateResult mutate a MirrorOriginSaveInfo directly, predating the domain
// model. Rather than move/rewrite that RNG-heavy generator, this WRAPS it: build a throwaway
// wire save from `run`, call the existing generator, then copy back only the fields it can
// touch. See the Task 5 brief - "the SAFE default is WRAP, leave MdMapGen where it is."
public static class MapGenerator
{
    // Port of the RecreateThemeFloorPoolMirrorDungeon handler body (:208-219). Re-rolls
    // run.ThemePools for the NEXT floor (Floor.Current.F + 1) and bumps the per-floor re-roll
    // counter. Before any floor has been selected (ThemeFloors empty) this is still the 8-option
    // floor-0 selection pool (PickThemes), not the 4-theme per-floor re-roll (RecreateThemes) -
    // capture-verified (see MdMapGen.PickThemes/RecreateThemes doc comments).
    // The re-rolls a floor allows are themePoolRecreateCount (1) plus Interstellar Travel's
    // 2/3/4 (StartBuffEffects.ThemeRefreshAllowance); past that the pool stays as it is and the
    // call returns false.
    // customThemes: the account's uploaded packs, which a re-roll can offer like any static pack.
    public static bool RecreateThemePool(Run run, IReadOnlyList<OpenLethe.Server.ThemeStatic>? customThemes = null)
    {
        if (run.TfpsCreated >= StartBuffEffects.ThemeRefreshAllowance(run)) return false;
        run.ThemePools = run.ThemeFloors.Count == 0
            ? OpenLethe.Server.MdMapGen.PickThemes(SharedRules.CurrentFloor(run), null, StartBuffEffects.ThemePoolSize(run), customThemes)
                .Select(t => new ThemePool { Idx = t.idx, Tfid = t.tfid, Egs = t.egs, Upegs = t.upegs })
                .ToList()
            : NextFloorPools(run, customThemes);
        run.TfpsCreated += 1;
        run.StartPools = new();
        return true;
    }

    // A normal-difficulty run: its latest pack came from an offer's normal group (wire idx 0), and
    // SelectThemeFloor stores that pick's idx in run.Idx. Once a hard pack (idx 1) is picked, the
    // offers carry no normal group any more, and idx only climbs (Infinite 2, Extreme 3) - so idx
    // 0 past floor 0 is normal.
    public static bool IsNormalMode(Run run) => run.ThemeFloors.Count > 0 && run.Idx == 0;

    // The pool for the floor after the current one - every re-roll path (refresh, the boss reward
    // pick or skip, a normal run's floor clear) goes through here, so all of them keep the run's
    // difficulty and skip the packs it already played. A normal run gets the same 8-pack shape as
    // the run start (4 normal idx 0 + 4 hard idx 1), so the screen's Normal/Hard switch works on
    // every floor; picking a hard pack sets run.Idx to 1 and the run is hard from then on (4 packs).
    // customThemes: the account's uploaded packs (see MdMapGen.PickThemes).
    public static List<ThemePool> NextFloorPools(Run run, IReadOnlyList<OpenLethe.Server.ThemeStatic>? customThemes = null)
    {
        var selectionFloor = run.Floor.Current.F + 1;
        var visited = run.ThemeFloors.Select(t => t.Tfid).ToHashSet();
        var count = StartBuffEffects.ThemePoolSize(run);
        var tfps = IsNormalMode(run)
            ? OpenLethe.Server.MdMapGen.PickThemes(selectionFloor, visited, count, customThemes)
            : OpenLethe.Server.MdMapGen.RecreateThemes(selectionFloor, visited, count, customThemes);
        return tfps
            .Select(t => new ThemePool { Idx = t.idx, Tfid = t.tfid, Egs = t.egs, Upegs = t.upegs })
            .ToList();
    }

    // Port of the SelectThemeFloorMirrorDungeon handler body (:236-291). `selectedIdx` is the
    // request's act/mode selector (used for the extreme-act new-theme gating below AND to set
    // the run's mode-enter counter, save.idx/run.Idx - the brief's one-arg signature omitted it,
    // but both are genuine game-state mutations that belong in Rules, not the handler).
    // customThemes: the account's uploaded packs (see MdMapGen.GenerateNewFloor).
    public static void GenerateFloor(Run run, long selectedThemeFloorId, long selectedIdx,
        IReadOnlyList<OpenLethe.Server.ThemeStatic>? customThemes = null)
    {
        // The floor being selected == the count of floors already recorded (ThemeFloors), NOT
        // SharedRules.CurrentFloor (LevelAdders) - see MirrorDungeonMap.cs:236-239.
        var floor = run.ThemeFloors.Count;

        var save = WireMapper.ToWire(run);
        OpenLethe.Server.MdMapGen.GenerateNewFloor(floor, selectedThemeFloorId, save, customThemes, selectedIdx);
        // GenerateNewFloor (+ its UpdateResult) only ever touches: dungeonMap.ns, currentInfo.cn,
        // currentInfo.eid, currentInfo.tfps, currentInfo.tfs, isEndDungeon, sepsCreated - write
        // exactly those back into `run`, never a blind whole-run re-import.
        run.Floor.Nodes = save.dungeonMap.ns.Select(n => new MapNode
        {
            F = n.f, S = n.s, Nid = n.nid, E = n.e, Eid = n.eid, Nnids = n.nnids,
        }).ToList();
        run.Floor.Current = new CurrentPosition { F = save.currentInfo.cn.f, S = save.currentInfo.cn.s, Nid = save.currentInfo.cn.nid };
        run.Eid = save.currentInfo.eid;
        run.ThemePools = save.currentInfo.tfps
            .Select(t => new ThemePool { Idx = t.idx, Tfid = t.tfid, Egs = t.egs, Upegs = t.upegs })
            .ToList();
        run.ThemeFloors = save.currentInfo.tfs
            .Select(t => new ThemeFloor { Idx = t.idx, F = t.f, Tfid = t.tfid, Egs = t.egs, Upegs = t.upegs, Ch = t.ch })
            .ToList();
        run.IsEndDungeon = save.isEndDungeon;
        run.SepsCreated = save.currentInfo.sepsCreated;
        RollSuperShop(run, floor, hard: selectedIdx != 0);
        run.HiddenNodeOrigins = new();

        // New-floor start: full-heal and revive the party (see SharedRules.ReviveAll).
        SharedRules.ReviveAll(run);

        // Start buffs that fire on reaching a floor: Cumulating Starcloud's Cost interest on every
        // move past floor 1, the gift popup of Moon Star-shop (floor 1) / Chance Comet (3/5/7).
        if (floor > 0) StartBuffEffects.OnMoveToNextFloor(run);
        StartBuffEffects.OnEnterFloor(run, floor);

        // "New floor theme" selection: in the extreme boss act (selectedIdx==3) the theme is
        // re-selected on alternating floors, and each such selection is a NEW floor theme that
        // increments Snft/Csnft and adds buff 102's SELECT_NEW_FLOOR_THEME level offset (+3, at
        // most +9 in all). Csnft is a per-floor flag: 1 on a new-theme floor, reset to 0 otherwise.
        // ponytail: the exact game rule for WHY act-3 themes alternate isn't encoded in the
        // static data - this reproduces both captured runs. Revisit if a deeper run differs.
        var efs = run.LevelOffsets;
        var offsets = StartBuffEffects.LevelOffsets(run);
        var levelBump = 0L;
        if (selectedIdx >= 3 && floor % 2 == 1)
        {
            efs.Snft += 1;
            efs.Csnft = 1;
            levelBump += offsets.NewThemeBonus(efs.Snft) - offsets.NewThemeBonus(efs.Snft - 1);
        }
        else efs.Csnft = 0;
        // Favor of the Nebulae++: "Upon entering Floor 6, all Identities gain 3 Level(s)".
        if (floor == 5) levelBump += offsets.EnterSixthFloor;
        if (levelBump != 0 || efs.Csnft == 1)
        {
            efs.Sbmlos += levelBump;
            var cap = efs.Sbmlos + efs.Egmlos;
            foreach (var u in run.Party) u.Mlos = cap;
        }

        // The per-floor theme re-roll counter resets to 0 on select (RecreateThemePool bumps it
        // during the floor). Verified 0 on every captured SelectThemeFloor response.
        run.TfpsCreated = 0;
        // ChoiceEventList = the newly-generated floor's event-node (e==3) eids, in map order.
        run.ChoiceEventList = run.Floor.Nodes
            .Where(n => n.F == floor && n.E == 3).Select(n => n.Eid).ToList();

        run.Idx = selectedIdx;
    }

    // Super shop odds, in percent: 3% plus 1% per 20 Cost spent at shops (run.UsedCost, wire
    // currentInfo.usedcost - the same number the client turns into the "next floor" chance), plus
    // Moon Star-shop's +10/+25% on top of those base odds (bonusPercent).
    public static long SuperShopPercent(long usedCost, long bonusPercent = 0) =>
        Math.Min(100, 3 + Math.Max(0, usedCost) / 20 + Math.Max(0, bonusPercent));

    // Turns the new floor's shop(s) into a super shop (eid 1) or a regular one (eid 0). Only hard
    // floors can roll one. The spend carries over from floor to floor and resets only when a
    // super shop generates - both captured runs (md-extreme-run*.jsonl) show usedcost dropping to
    // 0 on exactly the SelectThemeFloor responses whose new shop is eid 1, and kept otherwise.
    public static void RollSuperShop(Run run, long floor, bool hard)
    {
        var superShop = hard && Random.Shared.Next(100) < SuperShopPercent(run.UsedCost, StartBuffEffects.SuperShopBonusPercent(run));
        foreach (var node in run.Floor.Nodes.Where(n => n.F == floor && n.E == 10))
            node.Eid = superShop ? 1 : 0;
        if (superShop) run.UsedCost = 0;
    }
}
