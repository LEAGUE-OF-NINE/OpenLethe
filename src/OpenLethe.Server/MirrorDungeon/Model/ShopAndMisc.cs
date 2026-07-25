using System.Collections.Generic;

namespace OpenLethe.Server.MirrorDungeon.Model;

// A single shop slot (wire shop.slots[*] / ShopSlot). t = kind ("eg" ego gift, "up"
// personality upgrade), id = item id, s = availability (1 for sale, 0 sold out).
public sealed class ShopSlotState
{
    public string T { get; set; } = "";
    public long Id { get; set; }
    public long S { get; set; }
}

// The MD shop (wire currentInfo.shop / ShopInfo).
public sealed class ShopState
{
    public List<ShopSlotState> Slots { get; set; } = new();
    public long Rc { get; set; }
    public long Fre { get; set; }
    public long Fkre { get; set; }
    public long Cf { get; set; }
    public long Aec { get; set; }
    public long Aesp { get; set; }
}

// A rollable start-EGO-gift pool set (wire seps[*] / StartEgoGiftPoolSets).
public sealed class StartEgoGiftPool
{
    public long SetId { get; set; }
    public string Keyword { get; set; } = "";
    public List<long> Pool { get; set; } = new();
}

// Starlight/season progression state (wire currentInfo.slinfo / Slinfo).
public sealed class StarlightState
{
    // ponytail: pslp is taken verbatim from the capture (383, constant across the run);
    // its derivation is unknown (likely a prev-season/account value, not a per-run constant).
    public long Pslp { get; set; } = 383;
    public long Rsbp { get; set; }
    public long Rucc { get; set; }
    public List<long> Dpp { get; set; } = new();
    public List<long> Brcp { get; set; } = new();
    public long Pfb { get; set; }
    public long Scc { get; set; }
    public long Ieedt { get; set; }
    public List<long> Degids { get; set; } = new();
    public long Segr { get; set; }
    public long Ds { get; set; }
}

// MD mission progress (wire currentInfo.missions / Missions).
public sealed class MissionState
{
    public long Sr { get; set; }
    public long Sbe { get; set; }
    public List<long> Smuet { get; set; } = new();
    public long Sc { get; set; }
    public long Sec { get; set; } = -1;
    public long Nlm { get; set; } = 1;
    public long Sfs { get; set; }
    public long Sfb { get; set; }
    public long Ds { get; set; }
}

// A floor-boundary constraint difficulty score (wire cfs[*] / Cfs).
public sealed class ConstraintScore
{
    public long Floor { get; set; }
    public long Difficulty { get; set; }
}

// A floor-boundary constraint selection, appended by AcquireMirrorDungeonConstraints
// (wire scinfos[*] / Scinfo).
public sealed class ConstraintSelection
{
    public long Flooridx { get; set; }
    public List<long> Ids { get; set; } = new();
}

// A previously-used unit's upgrade indices (wire prevdul[*] / PrevUnitInfo).
public sealed class PrevUnit
{
    public long Pid { get; set; }
    public List<long> Upidx { get; set; } = new();
}

// A queued choice-event popup (wire pce[*] / ChoiceEventData). Nei is nullable and MUST
// stay null when unset — distinct bytes from a value.
public sealed class ChoiceEvent
{
    public List<long> Sl { get; set; } = new();
    public long Cs { get; set; }
    public long Ri { get; set; }
    public long? Nei { get; set; }
}

// An EGO skill stock entry (wire ess[*] / EgoSkillStock).
public sealed class SkillStock
{
    public string T { get; set; } = "";
    public long N { get; set; }
}

// A per-sinner MD statistic (wire outer statistics[*] / MdStatistics).
public sealed class SinnerStat
{
    public long Id { get; set; }
    public long Gd { get; set; }
    public long Rd { get; set; }
}

// Enemy level-adder ability info (wire currentInfo.egabilityinfo / EgAbilityInfo).
public sealed class EnemyAbility
{
    public long EnemyLevelAdder { get; set; }
}
