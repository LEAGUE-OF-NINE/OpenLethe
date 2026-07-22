using System.Collections.Generic;

namespace OpenLethe.Server.Wire;

// Server-authored ports of the Rust mirror-dungeon save-graph structs. Field names
// match Rust serde exactly. No [JsonIgnore]; lists init empty. Reuses Wire.{Currentnode,
// AcquiredEgogifts, ChoiceEventData, Element, Egos}.

public sealed class Dungeonunitlist1
{
    public List<long> upidx = new();
    public long mlos;
    public long pid;
    public long ch;
    public long cm;
    public long mhos;
    public long g;
    public long l;
    public List<Egos> es = new();
    public long isp;
}

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
    public List<Dungeonunitlist1> dul = new();
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
    public List<MdStatistics> statistics = new();
    public List<long> encounterstatistics = new();
    public long isEndDungeon;
    public long isReset;
    public long version;
}

public sealed class Egos2
{
    public long prevEgoId;
    public long nextEgoId;
}

public sealed class Formation
{
    // Rust field name typo `perv` (not `prev`) - kept verbatim, it's the wire contract.
    public long pervPersonalityId;
    public long nextPersonalityId;
    public List<Egos2> egos = new();
}

public sealed class MirrorDungeonHistories
{
    public long dungeonid;
    public List<object> restStatuses = new();
    public PrevPlayRecord prevPlayRecord = new();
}

public sealed class PrevPlayRecord
{
    public List<long> pids = new();
    public long epsId;
    public List<long> prevtfids = new();
}

public sealed class StartBuffInfo
{
    public long dungeonid;
    public List<long> bufstate = new();
    public List<long> enabled = new();
}

// ---- request params ----

public sealed class EnterMirrorDungeonParams
{
    public long dungeonid;
    public long idx;
}

public sealed class PurchaseHealMirrorDungeonParams
{
    public long idx;
    public long pid;
}

public sealed class PurchaseEgoGiftMirrorDungeonParams
{
    public long idx;
}

public sealed class SellEgoGiftMirrorDungeonParams
{
    public long id;
}

public sealed class UpgradeEgoGiftMirrorDungeonParams
{
    public long egoGiftId;
}

public sealed class AcquireRewardEgoGiftsMirrorDungeonParams
{
    public List<long> selectIndexList = new();
}

public sealed class SelectFormationMirrorDungeonParams
{
    public List<Formation> formation = new();
}

public sealed class PurchaseFormationMirrorDungeonParams
{
    public List<Formation> formation = new();
}

public sealed class AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeonParams
{
    public List<long> selectedEgoGiftIds = new();
}

public sealed class SelectThemeFloorMirrorDungeonParams
{
    public long selectedIdx;
    // Rust field name typo "Foor" (not "Floor") - kept verbatim, it's the wire contract.
    public long selectedThemeFoorId;
}

public sealed class EnterMirrorDungeonMapNodeParams
{
    public Currentnode currentnode = new();
}

public sealed class ExitMirrorDungeonMapNodeParams
{
    public Currentnode currentnode = new();
    public List<Dungeonunitlist1> dungeonunitlist = new();
    public long noderesult;
    public ChoiceEventData choiceEventData = new();
    public long isupdatedEgoSkillStock;
    public List<EgoSkillStock> egoSkillStockList = new();
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

public sealed class PurchaseHealMirrorDungeonResult
{
    public long cost;
    public List<Dungeonunitlist1> dungeonUnitList = new();
    public ShopInfo shopInfo = new();
    public long usedcost;
}

public sealed class PurchaseEgoGiftMirrorDungeonResult
{
    public long cost;
    public List<AcquiredEgogifts> egogifts = new();
    public ShopInfo shopInfo = new();
    public List<Dungeonunitlist1> dungeonUnitList = new();
    public long usedcost;
}

public sealed class SellEgoGiftMirrorDungeonResult
{
    public long cost;
    public List<AcquiredEgogifts> egogifts = new();
    public ShopInfo shopInfo = new();
    public List<Dungeonunitlist1> dungeonUnitList = new();
}

public sealed class UpgradeEgoGiftMirrorDungeonResult
{
    public long cost;
    public AcquiredEgogifts egoGift = new();
    public List<Dungeonunitlist1> dungeonUnitList = new();
    public long usedcost;
}

public sealed class AcquireRewardEgoGiftsMirrorDungeonResult
{
    public List<AcquiredEgogifts> egoGifts = new();
    public List<RemainRewardEvent> remainRewardEvent = new();
    public List<Dungeonunitlist1> dungeonUnitList = new();
    public MirrorOriginSaveInfo saveinfo = new();
}

public sealed class RejectRewardEgoGiftsMirrorDungeonResult
{
    public List<RemainRewardEvent> remainRewardEvent = new();
    public MirrorOriginSaveInfo saveinfo = new();
}

public sealed class AcquireMirrorDungeonExitRewardResult
{
    public List<Element> rewardList = new();
    public MirrorOriginSaveInfo saveInfo = new();
    public MirrorDungeonHistories history = new();
    public StartBuffInfo startBuffInfo = new();
}

public sealed class SelectFormationMirrorDungeonResult
{
    public MirrorOriginSaveInfo saveInfo = new();
}

public sealed class PurchaseFormationMirrorDungeonResult
{
    public long cost;
    public List<Dungeonunitlist1> dungeonUnitList = new();
    public ShopInfo shopInfo = new();
    public PrevUnitInfo prevUnitInfo = new();
    public long usedcost;
}

public sealed class AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeonResult
{
    public MirrorOriginSaveInfo saveInfo = new();
}

public sealed class RecreateThemeFloorPoolMirrorDungeonResult
{
    public MirrorOriginSaveInfo saveInfo = new();
}

public sealed class SelectThemeFloorMirrorDungeonResult
{
    public MirrorOriginSaveInfo saveInfo = new();
}

public sealed class EnterMirrorDungeonMapNodeResult
{
    public List<object> abnormalityLogs = new();
    public List<long> passingNodeIds = new();
    public Currentnode currentNode = new();
    public ShopInfo shopInfo = new();
    public List<AcquiredEgogifts> egogifts = new();
    public List<PrevUnitInfo> prevdul = new();
    public List<long> preves = new();
    public long nr;
    public long cost;
}

public sealed class ExitMirrorDungeonMapNodeResult
{
    public CurrentInfo currentInfo = new();
    public List<object> abnormalityLogs = new();
}

public sealed class UpdateMirrorDungeonMapNodeParams
{
    public Currentnode currentnode = new();
    public ChoiceEventData choiceEventData = new();
    public List<Dungeonunitlist1> dungeonUnitList = new();
    public List<AcquiredEgogifts> updatedEgoGifts = new();
}

public sealed class UpdateMirrorDungeonMapNodeResult
{
    public List<ChoiceEventData> prevChoiceEvent = new();
    public List<AcquiredEgogifts> currentEgoGifts = new();
    public List<Dungeonunitlist1> dungeonUnitList = new();
}

// ---- cycle 4e: rewards + fusion ----

public sealed class CombineEgoGiftMirrorDungeonParams
{
    public List<long> materialEgoGiftIds = new();
    public string keyword = "";
    public long isOrigin;
}

public sealed class CombineEgoGiftMirrorDungeonResult
{
    public AcquiredEgogifts resultEgoGift = new();
    public List<AcquiredEgogifts> resultEgoGifts = new();
    public bool isSuccess;
    public List<AcquiredEgogifts> egoGifts = new();
    public List<Dungeonunitlist1> dungeonUnitList = new();
}

public sealed class RefreshShopEgoGiftsMirrorDungeonParams
{
    public string keyword = "";
    public long isOrigin;
}

public sealed class RefreshShopEgoGiftsMirrorDungeonResult
{
    public long cost;
    public ShopInfo shopInfo = new();
    public long usedcost;
}

public sealed class GetMirrorDungeonEgoGiftRecordResult
{
    public List<long> acquiredegogifts = new();
    public List<long> themeFloorIds = new();
}

public sealed class ExitMirrorDungeonResult
{
    public long isEndDungeon;
    public long isclear;
    public List<MdStatistics> statistics = new();
}

public sealed class AcquireRewardEgoGiftsWithEnemyBufParams
{
    public List<long> selectIndexList = new();
    public long isOrigin;
}

public sealed class AcquireRewardEgoGiftsWithEnemyBufResult
{
    public List<AcquiredEgogifts> egoGifts = new();
    public List<RemainRewardEvent> remainRewardEvent = new();
    public List<Dungeonunitlist1> dungeonUnitList = new();
    public List<long> levelAdders = new();
    public MirrorOriginSaveInfo saveinfo = new();
}

public sealed class AcquireMirrorDungeonBattleRewardParams
{
    public List<long> selectIndexList = new();
    public long isOrigin;
}

public sealed class AcquireMirrorDungeonBattleRewardResult
{
    public MirrorOriginSaveInfo saveinfo = new();
}
