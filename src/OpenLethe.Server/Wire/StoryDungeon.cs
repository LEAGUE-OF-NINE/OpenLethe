using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace OpenLethe.Server.Wire;

// Server-authored ports of the Rust story-dungeon structs. Field names match Rust
// serde exactly. No [JsonIgnore]; lists init empty. Client-authored blobs (dul, ess,
// request unit/ego lists) are JsonNode passthrough - stored and echoed, never inspected.

public sealed class Currentnode
{
    public long f;
    public long s;
    public long nid;
}

public sealed class AcquiredEgogifts
{
    public long id;
    public List<long> pids = new();
    public long un;
    public long ul;
}

public sealed class ChoiceEventData
{
    public List<long> sl = new();
    public long cs;
    public long ri;
    public long? nei;
}

public sealed class Currentinfo
{
    public List<JsonNode> dul = new();
    public Currentnode scpn = new();
    public List<AcquiredEgogifts> scpegl = new();
    public List<long> opn = new();
    public Currentnode cn = new();
    public List<AcquiredEgogifts> egs = new();
    public List<long> pnids = new();
    public long nr;
    public List<ChoiceEventData> pce = new();
    public List<JsonNode> ess = new();
    public long dn;
}

public sealed class StorySaveInfo
{
    public long dungeonid;
    public Currentinfo currentinfo = new();
}

// ---- request params ----

public sealed class EnterStoryDungeonParams
{
    public long stageid;
    public List<JsonNode> personalities = new();
}

public sealed class EnterStoryDungeonMapNodeParams
{
    public long floornumber;
    public long sectornumber;
    public long nodeid;
}

public sealed class ExitStoryDungeonParams
{
    public long nodeid;
}

public sealed class ExitStoryDungeonMapNodeParams
{
    public List<AcquiredEgogifts> updatedEgoGifts = new();
    public List<JsonNode> dungeonunitlist = new();
    public List<JsonNode> egoSkillStockList = new();
}

// ---- response results ----

public sealed class EnterStoryDungeonResult
{
    public StorySaveInfo saveInfo = new();
    public List<object> nodesRecord = new();
}

public sealed class ReEnterStoryDungeonResult
{
    public StorySaveInfo saveInfo = new();
    public List<long> nodesRecord = new();
    public long isAllDie;
}

public sealed class EnterStoryDungeonMapNodeResult
{
    public Currentnode node = new();
    public long nr;
    public List<object> abnormalityLogs = new();
}

public sealed class ExitStoryDungeonMapNodeResult
{
    public StorySaveInfo saveInfo = new();
    public List<object> abnormalityLogs = new();
    public List<AcquiredEgogifts> acquiredEgogifts = new();
    public long isAllDie;
}

public sealed class ReturnSavePointStoryDungeonMapResult
{
    public Currentinfo currentInfo = new();
}

public sealed class ExitStoryDungeonResult
{
    public StorySaveInfo saveInfo = new();
    public bool iswin;
    public long cleartype;
    public long addexptouser;
    public List<object> personalityinfos = new();
    public List<Element> expticket = new();
    public List<Element> rewarditem = new();
    public List<Element> exrewarditem = new();
    public List<Element> firstrewarditem = new();
    public Element givebackstaminabyDefeat = new();
    public List<object> statistics = new();
    public bool isGacksung;
}
