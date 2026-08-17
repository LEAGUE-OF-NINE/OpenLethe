using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace OpenLethe.Server.Wire;

// Railway wire types. Shapes come from the client (packets/_shared.cs
// Railway*Format) reconciled field-for-field against the Refraction Railway
// capture in docs/flows(2) - the capture is the source of truth where the two
// drift (it carries `startdate`/`clearnumber`, which the bundled client header
// predates). PacketJson.Options has IncludeFields=true and no naming policy, so
// field names ARE the wire contract.

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
    public long sid;
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

/// One accumulated rotation buff inside a buff set (RailwayBuffFormat).
public sealed class RailwayBuff
{
    public long id;
    public long count;
    public List<long> targetids = new();
}

/// A dungeon's rotation buff set (RailwayBuffSetFormat). Which of `recentbuffid`
/// and `currentbuffids` is maintained depends on the set's static `selectOption`
/// - see RailwayRules.ApplyBuffSelection.
public sealed class Buffsets
{
    public long setid;
    public List<RailwayBuff> buffs = new();
    public long recentbuffid;
    public List<long> currentbuffids = new();
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
    public long sid;
    public long pord;
}

public sealed class Statistics1
{
    public long id;
    public long gd;
    public long rd;
}

public sealed class UpdateNodeDatas
{
    public long nodeid;
    public List<EgoSkillStock1> egostocks = new();
    public List<PrevStatusData> status = new();
    public long clearturn;
    public long playturn;
    public List<Statistics1> statistics = new();
    // Client-authored blobs the server only stores and echoes. `enemy` is
    // SaveDataForRailwayDungeon and `battleStates` RailwayBattleStateFormat[] in
    // the client; neither is ever read server-side, so they stay passthrough.
    public JsonNode enemy = new JsonObject();
    public List<JsonNode> battleStates = new();
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
    // Both dates are null (not "") until they happen - the capture sends JSON null.
    public string? firstcleardate;
    public string? startdate;
    public long clearnumber;
    public long lastenternodeid;
    public long currentclearrotation;
    public long lastclearrotation;
    public List<Buffsets> buffsets = new();
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
    public List<Buffsets> buffsets = new();
    public List<Buffsetsbyegogift> buffsetsbyegogift = new();
    // Only ever [] in the capture, and omitted entirely from the freshly built
    // currentLog ExitRailwayDungeon returns - we always emit it (the client
    // declares it) and mask that one omit-when-empty quirk in the replay.
    public List<JsonNode> battleStatesPerNode = new();
    public string date = "";
    public string? startdate;
    public long deadunitnumber;
    public long prevclearnode;
    public long currentnode;
}

// ---- request params ----

public sealed class EnterRailwayDungeonParams
{
    public long dungeonId;
    public List<Personalities>? personalities;
}

public sealed class EnterRailwayDungeonNodeParams
{
    public long dungeonId;
    public long nodeid;
    public List<long>? abnormalityids;
    public List<long>? participatedPIds;
    public List<JsonNode>? abnormalityLogs;
    public List<Personalities>? personalities;
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
    public List<PrevStatusData>? unitStatusList;
    public List<EgoSkillStock1>? egoSkillStockList;
    public List<JsonNode>? abnormalityLogs;
    public List<Statistics1>? statistics;
    public long clearTurn;
    public bool iswin;
    public JsonNode? enemy;
    public Buffsetsbyegogift? buffsetbyegogift;
    public List<JsonNode>? battleStates;
}

public sealed class ExitRailwayDungeonRestNodeParams
{
    public long dungeonId;
    public long nodeid;
    public List<Personalities>? personalities;
}

public sealed class SelectRailwayDungeonBuffParams
{
    public long dungeonId;
    public List<SelectedBuff>? selectedBuffs;
}

public sealed class SelectedBuff
{
    public long setId;
    public long buffId;
    public long targetId;
}

/// Note the lowercase `dungeonid` - the client declares this one differently
/// from every sibling railway packet (packets/api_GiveUpRailwayDungeonNodeInBattle.cs).
public sealed class GiveUpRailwayDungeonNodeInBattleParams
{
    public long dungeonid;
    public long nodeid;
    public List<JsonNode>? abnormalityLogs;
}

public sealed class AcquireRailwayDungeonRewardParams
{
    public long dungeonId;
}

public sealed class GetRailwayDungeonNodeAndLogAllParams
{
    public long dungeonId;
}

public sealed class GetRailwayDungeonExtraRewardStatesParams
{
    public List<long>? dungeonIds;
}

// ---- response results ----

public sealed class EnterRailwayDungeonResult
{
    public RailwaySaveInfo saveInfo = new();
    public UpdateNodeDatas startNodeData = new();
}

public sealed class EnterRailwayDungeonNodeResult
{
    public long nodeid;
    public List<long> deletedNodeIds = new();
    public List<AbnormalityLogEntry> abnormalityLogs = new();
    public List<PrevStatusData> prevStatusData = new();
    public List<EgoSkillStock1> prevEgoStockData = new();
    public JsonNode prevEnemyData = new JsonObject();
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

/// The client also declares a singular `nodeData` here; the real server does not
/// send it (checked against every ExitRailwayDungeonNode flow in the capture), so
/// neither do we - `updateNodeDatas` carries the same node.
public sealed class ExitRailwayDungeonNodeResult
{
    public List<AbnormalityLogEntry> abnormalityLogs = new();
    public RailwaySaveInfo saveInfo = new();
    public List<UpdateNodeDatas> updateNodeDatas = new();
}

public sealed class ExitRailwayDungeonRestNodeResult
{
    public RailwaySaveInfo saveInfo = new();
    public List<long> deletedNodeIds = new();
    public UpdateNodeDatas nodeData = new();
}

public sealed class SelectRailwayDungeonBuffResult
{
    public RailwaySaveInfo saveInfo = new();
    public UpdateNodeDatas nodeData = new();
}

public sealed class GiveUpRailwayDungeonNodeInBattleResult
{
    public UpdateNodeDatas nodeData = new();
    public List<AbnormalityLogEntry> abnormalityLogs = new();
}

public sealed class AcquireRailwayDungeonRewardResult
{
    public RailwaySaveInfo saveInfo = new();
    public List<Element> rewardList = new();
}

public sealed class GetRailwayDungeonNodeAndLogAllResult
{
    public RailwaySaveInfo railwaySaveInfo = new();
    public List<UpdateNodeDatas> nodeDatas = new();
    public List<CurrentLog> logDatas = new();
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
