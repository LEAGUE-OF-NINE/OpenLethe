using System;
using System.Collections.Generic;
using System.Linq;
using OpenLethe.Resources;

namespace OpenLethe.Server.MirrorDungeon.Data;

// Abnormality-unit -> body-part lookup (EnterMirrordungeonMapNodeBattleAfterChoice's `ps`).
// Scans the whole abnormality-unit folder (dozens of files, one per abno family) - collision
// -checked against the md-extreme capture: no id repeats with a different abnormalityPartList.
public sealed class AbnormalityUnit
{
    public long id;
    public List<long> abnormalityPartList = new();
}

public static class MdAbnoUnits
{
    private static readonly Lazy<List<AbnormalityUnit>> All =
        new(() => StaticData.GetList<AbnormalityUnit>("static-data/abnormality-unit"));

    public static List<long> PartsFor(long abnoId) =>
        All.Value.FirstOrDefault(u => u.id == abnoId)?.abnormalityPartList ?? new List<long>();
}
