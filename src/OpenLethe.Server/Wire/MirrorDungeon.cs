using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace OpenLethe.Server.Wire;

// Server-authored ports of the Rust mirror-dungeon save-graph structs. Field names
// match Rust serde exactly. No [JsonIgnore]; lists init empty. Reuses Wire.{Currentnode,
// AcquiredEgogifts, ChoiceEventData, Element}. Client-authored dul is JsonNode passthrough.

public sealed class MdStatistics // Rust `Statistics`; renamed to avoid ambiguity (wire field name is unaffected)
{
    public long id;
    public long gd;
    public long rd;
}

public sealed class EgoSkillStock
{
    public string t = "";
    public long n;
}

public sealed class Ns
{
    public long f;
    public long s;
    public long nid;
    public long e;
    public long eid;
    public List<long> nnids = new();
}

public sealed class DungeonMap
{
    public List<Ns> ns = new();
}

public sealed class Cfs
{
    public long floor;
    public long difficulty;
}

public sealed class Efs
{
    public long rpf;
}

public sealed class Pcs
{
    public long pid;
    public long so;
}

public sealed class ShopInfo
{
    public long ph;
    public long pup;
    public long upid;
    public List<long> peg = new();
    public long pcf;
    public List<long> egpool = new();
    public long rc;
    public long fre;
    public long fkre;
    public long aesp;
    public List<Pcs> pcs = new();
}

public sealed class StartEgoGiftPoolSets
{
    public long setId;
    public string keyword = "";
    public List<long> pool = new();
}

public sealed class Tfs
{
    public long f;
    public long tid;
    public long idx;
    public long tfid;
    public List<long> egs = new();
}

public sealed class Tfps
{
    public long idx;
    public long tfid;
    public List<long> egs = new();
    public List<long> upegs = new();
}

public sealed class RemainRewardEvent
{
    public string rt = "";
    public long se;
    public long sh;
    public List<long> pool = new();
    public List<long> pool_v2 = new();
    public List<long> pool_v3 = new();
}

public sealed class PrevUnitInfo
{
    public long pid;
    public List<long> upidx = new();
}

public sealed class CurrentInfo
{
    public long eid;
    public List<JsonNode> dul = new();
    public long sepsId;
    public List<StartEgoGiftPoolSets> seps = new();
    public long sepsCreated;
    public List<Tfs> tfs = new();
    public List<Tfps> tfps = new();
    public long tfpsCreated;
    public List<RemainRewardEvent> rre = new();
    public long ri;
    public long cost;
    public long usedcost;
    public ShopInfo shop = new();
    public List<PrevUnitInfo> prevdul = new();
    public List<long> preves = new();
    public List<long> leveladders = new();
    public string startKeyword = "";
    public long startBufPoint;
    public List<Cfs> cfs = new();
    public Efs efs = new();
    public Currentnode cn = new();
    public List<AcquiredEgogifts> egs = new();
    public List<long> pnids = new();
    public long nr;
    public List<ChoiceEventData> pce = new();
    public List<EgoSkillStock> ess = new();
    public long dn;
}

public sealed class MirrorOriginSaveInfo
{
    public long dungeonId;
    public long idx;
    public CurrentInfo currentInfo = new();
    public DungeonMap dungeonMap = new();
    public List<long> choiceEventList = new();
    public long addUserExp;
    public List<MdStatistics> statistics = new();
    public List<long> encounterstatistics = new();
    public long isEndDungeon;
    public long isReset;
    public long version;
}

// ---- request params ----

public sealed class EnterMirrorDungeonParams
{
    public long dungeonid;
    public long idx;
}

// ---- response results ----

public sealed class EnterMirrorDungeonResult
{
    public MirrorOriginSaveInfo saveInfo = new();
    public List<object> recentCharacterList = new();
}

public sealed class ReEnterMirrorDungeonResult
{
    public MirrorOriginSaveInfo saveInfo = new();
}
