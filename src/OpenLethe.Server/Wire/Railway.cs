using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace OpenLethe.Server.Wire;

// Server-authored ports of lethe-server/models/src/types.rs railway structs. Field
// names are the wire contract (match Rust serde exactly). All Rust fields use
// #[serde(default)] (always serialized), so no [JsonIgnore]; lists/strings init to
// empty to serialize as []/"" not null. PacketJson.Options has IncludeFields=true.

public sealed class Egos
{
    public long id;
    public long g;
    public long idx;
}

public sealed class Personalities
{
    public long pid;
    public long g;
    public long l;
    public List<Egos> es = new();
    public long sp;
    public long gi;
    public long pord;
}

public sealed class Extrarewardstate
{
    public long id;
    public bool isRewarded;
}

public sealed class Buffs1
{
    public long buffId;
    public long playeregogift;
    public long enemyegogift;
}

public sealed class Buffsetsbyegogift
{
    public long nid;
    public List<Buffs1> buffs = new();
}

public sealed class EgoSkillStock1
{
    public string t = "";
    public long n;
}

public sealed class Sin
{
    public List<long> sp = new();
    public List<long> cs = new();
    public long rs;
}

public sealed class PrevStatusData
{
    public long pid;
    public long hp;
    public long mp;
    public long isp;
    public Sin sin = new();
    public List<Egos> egos = new();
    public long sp;
    public long lv;
    public long g;
    public long gi;
    public long pord;
}

public sealed class Statistics1
{
    public long id;
    public long gd;
    public long rd;
}

public sealed class PrevEnemyData
{
    public long lastWave;
    public long lastTurn;
    // client-authored, only stored/echoed - passthrough (no AbnoSaveData/PartsData port)
    public List<JsonNode> abnoSaveDataList = new();
}

public sealed class UpdateNodeDatas
{
    public long nodeid;
    public List<EgoSkillStock1> egostocks = new();
    public List<PrevStatusData> status = new();
    public long clearturn;
    public long playturn;
    public List<Statistics1> statistics = new();
    public PrevEnemyData enemy = new();
    public long nodestate;
}

public sealed class Turnspernode
{
    public long nid;
    public long turn;
}

public sealed class Detailstatistics
{
    public long collectionId;
    public List<Personalities> personalities = new();
    public List<Statistics1> statistics = new();
}

public sealed class RailwaySaveInfo
{
    public long id;
    public long prevclearnode;
    public long currentnode;
    public long lastclearnode;
    public List<Personalities> personalities = new();
    public long payreward;
    public long rewardstate;
    public List<Extrarewardstate> extrarewardstate = new();
    public string firstcleardate = "";
    public long currentclearrotation;
    public long lastenternodeid;
    public long lastclearrotation;
    // ponytail: buffsets is only populated by the out-of-scope SelectRailwayDungeonBuff
    // handler; empty here. Port the Buffsets type when that handler lands.
    public List<object> buffsets = new();
    public List<Buffsetsbyegogift> buffsetsbyegogift = new();
    public long initseed;
    public long currentseed;
}

public sealed class CurrentLog
{
    public long idx;
    public List<Personalities> personalities = new();
    public List<Statistics1> statistics = new();
    public List<Detailstatistics> detailstatistics = new();
    public long clearturn;
    public List<Turnspernode> turnspernode = new();
    public long clearrotation;
    public List<object> buffsets = new();
    public List<Buffsetsbyegogift> buffsetsbyegogift = new();
    public string date = "";
    public long deadunitnumber;
}

// ---- request params (mirror Rust RequestParamApi*) ----

public sealed class EnterRailwayDungeonParams
{
    public long dungeonId;
    public List<Personalities> personalities = new();
}

public sealed class EnterRailwayDungeonNodeParams
{
    public long dungeonId;
    public long nodeid;
}

public sealed class ExitRailwayDungeonParams
{
    public long dungeonId;
    public bool isClear;
}

public sealed class ExitRailwayDungeonNodeParams
{
    public long dungeonId;
    public long nodeid;
    public List<PrevStatusData> unitStatusList = new();
    public List<EgoSkillStock1> egoSkillStockList = new();
    public List<Statistics1> statistics = new();
    public long clearTurn;
    public bool iswin;
    public PrevEnemyData enemy = new();
    public Buffsetsbyegogift buffsetbyegogift = new();
}

public sealed class ExitRailwayDungeonRestNodeParams
{
    public long dungeonId;
    public long nodeid;
    public List<Personalities> personalities = new();
}

public sealed class GetRailwayDungeonNodeAndLogAllParams
{
    public long dungeonId;
}

// ---- response results (mirror Rust ResponseResultApi*) ----

public sealed class EnterRailwayDungeonResult
{
    public RailwaySaveInfo saveInfo = new();
    public UpdateNodeDatas startNodeData = new();
}

public sealed class EnterRailwayDungeonNodeResult
{
    public long nodeid;
    public List<long> deletedNodeIds = new();
    public List<object> abnormalityLogs = new();
    public List<PrevStatusData> prevStatusData = new();
    public List<EgoSkillStock1> prevEgoStockData = new();
    public PrevEnemyData prevEnemyData = new();
    public long prevClearNodeId;
    public long currentNodeId;
    public List<Buffsetsbyegogift> buffsetsbyegogift = new();
}

public sealed class ExitRailwayDungeonResult
{
    public bool isclear;
    public RailwaySaveInfo saveInfo = new();
    public CurrentLog currentLog = new();
    public List<Element> rewards = new();
}

public sealed class ExitRailwayDungeonNodeResult
{
    public RailwaySaveInfo saveInfo = new();
    public List<object> abnormalityLogs = new();
    public UpdateNodeDatas nodeData = new();
    public List<UpdateNodeDatas> updateNodeDatas = new();
}

public sealed class ExitRailwayDungeonRestNodeResult
{
    public RailwaySaveInfo saveInfo = new();
    public List<long> deletedNodeIds = new();
    public UpdateNodeDatas nodeData = new();
}

public sealed class GetRailwayDungeonNodeAndLogAllResult
{
    // Client-declared but absent from Rust's ResponseResultApi...::new(node_data, Vec::new()).
    // Client wins on shape; additive, so nothing reading the old response breaks.
    public RailwaySaveInfo railwaySaveInfo = new();
    public List<UpdateNodeDatas> nodeDatas = new();
    public List<CurrentLog> logDatas = new();
}

// ---- client-only granular getters (no Rust reference; shapes from packets/api_*.cs) ----

public sealed class GetRailwayDungeonExtraRewardStatesParams
{
    public List<long>? dungeonIds;
}

public sealed class GetRailwayDungeonSaveInfoResult
{
    public RailwaySaveInfo railwaySaveInfo = new();
}

public sealed class GetRailwayDungeonNodeDatasResult
{
    public List<UpdateNodeDatas> nodeDatas = new();
}

public sealed class GetRailwayDungeonLogsResult
{
    // ponytail: nothing in the port persists Railway logs - ExitRailwayDungeon builds a
    // CurrentLog and returns it without storing it, and GetRailwayDungeonNodeAndLogAll
    // likewise answers with an empty list. Populate here when logs gain storage.
    public List<CurrentLog> logDatas = new();
}

public sealed class ExtraRewardStateByDungeonId
{
    public long dungeonId;
    public List<Extrarewardstate> extraRewardState = new();
}

public sealed class GetRailwayDungeonExtraRewardStatesResult
{
    public List<ExtraRewardStateByDungeonId> list = new();
}
