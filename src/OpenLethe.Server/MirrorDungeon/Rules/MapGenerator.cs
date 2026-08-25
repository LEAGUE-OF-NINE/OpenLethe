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
    // The MD7 start buffs that grant personality level-cap offsets (verbatim port of
    // MirrorDungeonMap.cs LevelOffsets :30 / StartRunRules.cs :20 - same fixed set, see there
    // for why it's fixed rather than read from the per-run enabled buffs).
    private static readonly MdLevelOffsets LevelOffsets = MdStartBuffs.LevelOffsets(new long[] { 106, 102 });

    // Port of the RecreateThemeFloorPoolMirrorDungeon handler body (:208-219). Re-rolls
    // run.ThemePools for the NEXT floor (Floor.Current.F + 1) and bumps the per-floor re-roll
    // counter. Before any floor has been selected (ThemeFloors empty) this is still the 8-option
    // floor-0 selection pool (PickThemes), not the 4-theme per-floor re-roll (RecreateThemes) -
    // capture-verified (see MdMapGen.PickThemes/RecreateThemes doc comments).
    public static void RecreateThemePool(Run run)
    {
        var tfps = run.ThemeFloors.Count == 0
            ? OpenLethe.Server.MdMapGen.PickThemes(SharedRules.CurrentFloor(run))
            : OpenLethe.Server.MdMapGen.RecreateThemes(run.Floor.Current.F + 1);
        run.ThemePools = tfps
            .Select(t => new ThemePool { Idx = t.idx, Tfid = t.tfid, Egs = t.egs, Upegs = t.upegs })
            .ToList();
        run.TfpsCreated += 1;
        run.StartPools = new();
    }

    // Port of the SelectThemeFloorMirrorDungeon handler body (:236-291). `selectedIdx` is the
    // request's act/mode selector (used for the extreme-act new-theme gating below AND to set
    // the run's mode-enter counter, save.idx/run.Idx - the brief's one-arg signature omitted it,
    // but both are genuine game-state mutations that belong in Rules, not the handler).
    public static void GenerateFloor(Run run, long selectedThemeFloorId, long selectedIdx)
    {
        // The floor being selected == the count of floors already recorded (ThemeFloors), NOT
        // SharedRules.CurrentFloor (LevelAdders) - see MirrorDungeonMap.cs:236-239.
        var floor = run.ThemeFloors.Count;

        var save = WireMapper.ToWire(run);
        OpenLethe.Server.MdMapGen.GenerateNewFloor(floor, selectedThemeFloorId, save);
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

        // New-floor start: full-heal the party. Every unit's CurrentHp -> 10000; a unit revived
        // from 0 HP also has its Cm (corrosion) cleared - live units keep their Cm unchanged.
        foreach (var u in run.Party)
        {
            if (u.CurrentHp == 0) u.Cm = 0;
            u.CurrentHp = 10000;
        }

        // "New floor theme" selection: in the extreme boss act (selectedIdx==3) the theme is
        // re-selected on alternating floors, and each such selection is a NEW floor theme that
        // increments Snft/Csnft and adds buff 102's SELECT_NEW_FLOOR_THEME level offset (+3).
        // Csnft is a per-floor flag: 1 on a new-theme floor, reset to 0 otherwise.
        // ponytail: the exact game rule for WHY act-3 themes alternate isn't encoded in the
        // static data - this reproduces both captured runs. Revisit if a deeper run differs.
        var efs = run.LevelOffsets;
        if (selectedIdx >= 3 && floor % 2 == 1)
        {
            efs.Snft += 1;
            efs.Csnft = 1;
            efs.Sbmlos += LevelOffsets.PerNewTheme;
            var cap = efs.Sbmlos + efs.Egmlos;
            foreach (var u in run.Party) u.Mlos = cap;
        }
        else efs.Csnft = 0;

        // The per-floor theme re-roll counter resets to 0 on select (RecreateThemePool bumps it
        // during the floor). Verified 0 on every captured SelectThemeFloor response.
        run.TfpsCreated = 0;
        // ChoiceEventList = the newly-generated floor's event-node (e==3) eids, in map order.
        run.ChoiceEventList = run.Floor.Nodes
            .Where(n => n.F == floor && n.E == 3).Select(n => n.Eid).ToList();

        run.Idx = selectedIdx;
    }
}
