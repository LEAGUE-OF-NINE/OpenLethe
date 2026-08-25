using OpenLethe.Resources;

namespace OpenLethe.Server.MirrorDungeon.Data;

// Start-of-run buff catalog + point economy. Sourced from
// mirrordungeon-start-buffs/mirrordungeon-start-buffs-07.json list[0]: buffPoint.basePoint
// (60) is the spendable budget, remainPointToCostMultiplier (5) converts spent buff points
// into the in-game `cost` currency EnableStartBuff charges.
public sealed class MdStartBuffPoint
{
    public long basePoint;
    public long remainPointToCostMultiplier;
}

public sealed class MdStartBuffEffect
{
    public string type = "";
    public long value;
    public long value2;
}

public sealed class MdStartBuffEntry
{
    public long id;
    public long cost;
    public List<MdStartBuffEffect> effects = new();
}

// Personality level-cap offsets contributed by a set of enabled start buffs, read from each
// buff's effects[] (mirrordungeon-start-buffs-07.json). Replaces the old hardcoded base-3 /
// cap-8 sbmlos:
//   sbmlos = EnterFirstFloor + min(floorClears, ClearCap)*PerClear + snft*PerNewTheme
// (ENTER_1ST_FLOOR seeds the floor-1 cap, CLEAR_FLOOR adds `value` per clear up to `value2`
// clears, SELECT_NEW_FLOOR_THEME adds `value` per new-theme floor).
public readonly record struct MdLevelOffsets(long EnterFirstFloor, long PerClear, long ClearCap, long PerNewTheme)
{
    public long SbmlosAt(long floorClears, long snft) =>
        EnterFirstFloor + Math.Min(floorClears, ClearCap) * PerClear + snft * PerNewTheme;
}

public sealed class MdStartBuffDungeon
{
    public long dungeonId;
    public MdStartBuffPoint buffPoint = new();
    public List<MdStartBuffEntry> buffs = new();
}

public static class MdStartBuffs
{
    private static readonly Lazy<MdStartBuffDungeon?> Data = new(() =>
        StaticData.GetListFromFile<MdStartBuffDungeon>("static-data/mirrordungeon-start-buffs/mirrordungeon-start-buffs-07.json")
            .FirstOrDefault());

    public static long BasePoint => Data.Value?.buffPoint.basePoint ?? 0;

    // Sum of the picked buffs' raw point cost (NOT yet converted to the in-game `cost`
    // currency). Capture-verified: buffs [106,102] cost 40+20=60 (== BasePoint exactly).
    public static long RawSpend(IEnumerable<long> buffIds)
    {
        var d = Data.Value;
        return d is null ? 0 : buffIds.Sum(id => d.buffs.FirstOrDefault(b => b.id == id)?.cost ?? 0);
    }

    // Converts leftover buff points into the in-game `cost` currency.
    public static long PointToCostMultiplier => Data.Value?.buffPoint.remainPointToCostMultiplier ?? 0;

    // ADDITIONAL_START_COST added to the run's `cost` budget at AcquireStart. The enabled buff
    // ids aren't persisted in any save field, but the AcquireStart request reveals them: each
    // buff carrying ADDITIONAL_START_EGO_GIFT_SELECT lets the player pick one EXTRA starting ego
    // gift, so the count of extra selections (selectedEgoGiftIds - 1) identifies exactly which
    // cost buffs were enabled. Capture-verified: run-1 picks 1 gift (0 extra -> +0), run-2 picks
    // 2 (1 extra -> buff103's +400).
    // ponytail: this is a RECONSTRUCTION - it assumes every ADDITIONAL_START_EGO_GIFT_SELECT buff
    // also carries ADDITIONAL_START_COST (true for the only such buff, 103). A select-only or a
    // cost-only start buff would mis-derive; the only real fix is persisting the enabled buff ids,
    // which the harness can't do (a new save field leaks into every echoed response - see PB-4).
    public static long AdditionalStartCost(int extraSelects)
    {
        var d = Data.Value;
        if (d is null || extraSelects <= 0) return 0;
        return d.buffs
            .Where(b => b.effects.Any(e => e.type == "ADDITIONAL_START_EGO_GIFT_SELECT"))
            .Take(extraSelects)
            .Sum(b => b.effects.Where(e => e.type == "ADDITIONAL_START_COST").Sum(e => e.value));
    }

    // Reads the personality level-cap offsets from the enabled buffs' effects[]. Generalizes
    // over the three level-offset effect types (any unrecognized effect is ignored). Verified
    // against buffs [106,102]: ENTER_1ST_FLOOR 3, CLEAR_FLOOR value 1 / value2 5, and
    // SELECT_NEW_FLOOR_THEME 3 -> (3, 1, 5, 3), i.e. base 3, +1/clear capped at +5 (=8), +3/new
    // theme floor.
    public static MdLevelOffsets LevelOffsets(IEnumerable<long> buffIds)
    {
        var d = Data.Value;
        long enter = 0, perClear = 0, clearCap = 0, perNewTheme = 0;
        if (d is not null)
            foreach (var id in buffIds)
            {
                var b = d.buffs.FirstOrDefault(x => x.id == id);
                if (b is null) continue;
                foreach (var e in b.effects)
                    switch (e.type)
                    {
                        case "ADDITIONAL_PERSONALITY_LEVEL_ON_ENTER_1ST_FLOOR": enter += e.value; break;
                        case "ADDITIONAL_PERSONALITY_LEVEL_ON_CLEAR_FLOOR": perClear += e.value; clearCap += e.value2; break;
                        case "ADDITIONAL_PERSONALITY_LEVEL_ON_SELECT_NEW_FLOOR_THEME": perNewTheme += e.value; break;
                    }
            }
        return new MdLevelOffsets(enter, perClear, clearCap, perNewTheme);
    }
}
