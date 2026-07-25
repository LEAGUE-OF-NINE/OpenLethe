using System.Collections.Generic;

namespace OpenLethe.Server.MirrorDungeon.Model;

// The party's current position (wire cn / Currentnode).
public sealed class CurrentPosition
{
    public long F { get; set; }
    public long S { get; set; }
    public long Nid { get; set; }
}

// One node on the generated floor map (wire dungeonMap.ns[*] / Ns).
public sealed class MapNode
{
    public long F { get; set; }
    public long S { get; set; }
    public long Nid { get; set; }
    public long E { get; set; }              // node kind (event/shop/battle/...)
    public long Eid { get; set; }
    public List<long> Nnids { get; set; } = new();
}

// The current floor: where the party is + the generated node graph.
public sealed class Floor
{
    public CurrentPosition Current { get; set; } = new();
    public List<MapNode> Nodes { get; set; } = new();   // dungeonMap.ns
}

// Level-offset accumulators (wire efs / Efs).
public sealed class LevelOffsets
{
    public long Sbmlos { get; set; }
    public long Egmlos { get; set; }
    public long Snft { get; set; }
    public long Csnft { get; set; }
}
