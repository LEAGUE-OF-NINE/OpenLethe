using System.Collections.Generic;

namespace OpenLethe.Server.MirrorDungeon.Model;

// One sinner in the run's party (wire dul[*]). Opaque codes (Cm/Mhos/Isp/Sid/Mlos) kept
// verbatim per the naming discipline — meaning not yet established.
public sealed class PartyUnit
{
    public long PersonalityId { get; set; }   // pid
    public long CurrentHp { get; set; }        // ch (0-10000 scale)
    public long Cm { get; set; }               // cm
    public long Mhos { get; set; }             // mhos
    public long Gacksung { get; set; }         // g (per-identity awakening investment)
    public long Level { get; set; }            // l
    public List<EgoSkill> EgoSkills { get; set; } = new(); // es
    public long Isp { get; set; }              // isp
    public long Sid { get; set; }              // sid
    public long Mlos { get; set; }             // mlos (per-unit level-offset sum)
    public List<long> UpgradeIndices { get; set; } = new(); // upidx
    public long Pord { get; set; } = -1;       // pord (server slot order; capture always -1)
}

// A slotted E.G.O skill on a unit (wire es[*] / Egos).
public sealed class EgoSkill
{
    public long Id { get; set; }
    public long G { get; set; }
    public long Idx { get; set; }
}
