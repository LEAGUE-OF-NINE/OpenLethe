using System.Collections.Generic;
using System.Text.Json.Serialization;

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
    public long sid;
    public long pord = -1; // server-computed slot order; the client never sends it, the capture is always -1.
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
    public long sbmlos;
    public long egmlos;
    public long snft;
    public long csnft;
}

public sealed class Missions
{
    public long sr;
    public long sbe;
    public List<long> smuet = new();
    public long sc;
    public long sec = -1;
    public long nlm = 1;
    public long sfs;
    public long sfb;
    public long ds;
}

public sealed class Slinfo
{
    // ponytail: pslp is taken verbatim from the capture (383, constant across the run);
    // its derivation is unknown (likely a prev-season/account value, not a per-run constant).
    public long pslp = 383;
    public long rsbp;
    public long rucc;
    public List<long> dpp = new();
    public List<long> brcp = new();
    public long pfb;
    public long scc;
    public long ieedt;
    public List<long> degids = new();
    public long segr;
    public long ds;
}

public sealed class EgAbilityInfo
{
    public long enemyleveladder;
}

// Slots-based shop, matching the client contract UserMirrorDungeonShopDataFormat_NEW and
// the capture. Each slot is one shop item: t = kind ("eg" ego gift, "up" personality
// upgrade), id = the item id, s = availability (1 = for sale, 0 = sold out). Field order
// and names match the wire capture exactly.
public sealed class ShopSlot
{
    public string t = "";
    public long id;
    public long s;
}

public sealed class ShopInfo
{
    public List<ShopSlot> slots = new();
    public long rc;
    public long fre;
    public long fkre;
    public long cf;
    public long aec;
    public long aesp;
}

public sealed class StartEgoGiftPoolSets
{
    public long setId;
    public string keyword = "";
    public List<long> pool = new();
}

public sealed class Tfs
{
    public long idx;
    public long f;
    public long tfid;
    public List<long> egs = new();
    public List<long> upegs = new();

    // Fixture-confirmed (tests/OpenLethe.Tests/fixtures/md-extreme-run.jsonl emits this field
    // on tfs entries) but absent from the decompiled RandomDungeonMapThemeFormat contract in
    // packets/_shared.cs (f, tid, idx, tfid, egs, upegs only) - a decompile gap, not a wire error.
    public long ch;
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
    // Nullable/omit-when-null (unlike this file's usual "no [JsonIgnore]" convention):
    // the e==3 confirmed-gift rre entry (GetConfirmedEgogiftOnWinBattle from a choice-event
    // chain) OMITS these keys entirely on every one of the 18 captured records, while the
    // SAME rt from an e==14 abno battle (seq146) serializes them present as []. Two distinct
    // server-side construction paths for the same rt - leave these null (unset) when building
    // the e==3 branch, explicit `= new()` everywhere else, same as before.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<long>? pool_v2;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<long>? pool_v3;
}

public sealed class PrevUnitInfo
{
    public long pid;
    public List<long> upidx = new();
}

// A floor-boundary constraint selection, appended by AcquireMirrorDungeonConstraints.
public sealed class Scinfo
{
    public long flooridx;
    public List<long> ids = new();
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
    // Fields present in the capture but previously unmodelled. Empty/default at ENTER; later
    // tasks populate them as the run progresses.
    public List<long> peids = new();
    public List<long> phbids = new();
    public long etype;
    public List<long> upids = new();
    public List<long> spid = new();
    public Missions missions = new();
    public List<object> cels = new();
    public Slinfo slinfo = new();
    public long rentalid;
    public List<Scinfo> scinfos = new();
    public EgAbilityInfo egabilityinfo = new();
    public long isegr;
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
    public string startdate = ""; // server-set wall-clock run start; non-deterministic (masked in replay).
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
    // Fixture-confirmed (md-extreme-run.jsonl seq321) but absent from the decompiled
    // MirrorDungeonPrevPlayRecordFormat contract in packets/_shared.cs - a decompile gap,
    // same class as Tfs.ch above. Unused: `history` (this type's container) is masked
    // wholesale as per-account/lifetime state, but the field is modelled for completeness.
    public List<long> tfids = new();
}

public sealed class StartBuffInfo
{
    public long dungeonid;
    // Client wire type (MirrorDungeonStartBuffInfoFormat) declares a `bufstate` field too,
    // but it never appears in the capture (GetStartBuffFInfo/EnableStartBuff both echo just
    // {dungeonid, enabled}) - omitted rather than serialized-empty to byte-match.
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

public sealed class PurchaseUpgradePersonalityMirrorDungeonParams
{
    public long pid;
    public long idx;
    public bool isDetected;
    public bool useStarlight;
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
    public long selectedSetId;
    public List<long> selectedEgoGiftIds = new();
    public bool isEnableEgogiftDetectionToggle;
}

public sealed class GetStartBuffFInfoMirrorDungeonParams
{
    public long dungeonid;
}

public sealed class EnableStartBuffMirrorDungeonParams
{
    public long dungeonid;
    public List<long> buffids = new();
    public bool enableStarlight;
    public bool enableConvertedCost;
}

public sealed class DetectMirrorDungeonEgogiftByStarlightParams
{
    public List<long> egogiftIds = new();
}

public sealed class AcquireMirrorDungeonConstraintsParams
{
    public List<long> selectIdxList = new();
}

public sealed class EnterMirrordungeonMapNodeBattleAfterChoiceParams
{
    public long isOrigin;
    public List<long> participatedPids = new();
    public List<long> abnormalityids = new();
    public List<Dungeonunitlist1> dungeonUnitList = new();
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
    // The client sends skeleton abno logs (ids + empty atrr/atkr); the server fills the RNG
    // content and echoes them back sorted by id. Kept as raw nodes - only id/structure is
    // reproduced deterministically; the rolled fields are masked in the replay.
    public List<System.Text.Json.Nodes.JsonNode> abnormalityLogs = new();
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

public sealed class PurchaseUpgradePersonalityMirrorDungeonResult
{
    public long cost;
    public long usedcost;
    public List<Dungeonunitlist1> dungeonUnitList = new();
    public ShopInfo shopInfo = new();
    public Slinfo starlightInfo = new();
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

// One selectable exit-reward option (Preview returns 4; Acquire grants one of them).
// Field order matches the capture exactly (starlightConsumption BEFORE moduleConsumption -
// the decompiled MirrorDungeonExitRewardFormat contract in packets/_shared.cs declares the
// opposite order, a decompile-vs-wire mismatch like the PacketId casing artifacts - JsonDiff
// compares by key name, not position, so this is cosmetic, but kept capture-accurate).
public sealed class ExitRewardOption
{
    public long chanceConsumption;
    public List<Element> rewardList = new();
    public long starlightConsumption;
    public long moduleConsumption;
    public long mdpassOriginalAmount;
    public long mdpassCurrentChanceUsage;
}

public sealed class PreviewMirrorDungeonExitRewardResult
{
    public List<ExitRewardOption> rewardList = new();
    public long totalConstraintScore;
}

public sealed class AcquireMirrorDungeonExitRewardParams
{
    public bool useEnkephalinModule;
    public long chanceConsumption;
}

public sealed class AcquireMirrorDungeonExitRewardResult
{
    public List<Element> rewardList = new();
    public MirrorOriginSaveInfo saveInfo = new();
    public MirrorDungeonHistories history = new();
    // Fixture-confirmed (seq321) but absent from the decompiled ResPacket_AcquireMirrorDungeon
    // ExitReward contract - same decompile-gap class as PrevPlayRecord.tfids above. The run's
    // enabled start-buff ids; no persisted save field carries them (EnableStartBuff doesn't
    // write into MirrorOriginSaveInfo - adding one there would leak into every OTHER endpoint
    // that echoes currentInfo/saveInfo verbatim). Masked in the replay - see ReplayMasks.
    public List<long> enabledStartbufIds = new();
    public StartBuffInfo startBuffInfo = new();
    public long mdpassOriginalAmount;
    public long mdpassCurrentChanceUsage;
    public long totalConstraintScore;
    public long starlightChangeAmount;
    public List<long> currentClearedConstraintIds = new();
    public long lastclearedFloor;
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

public sealed class GetStartBuffFInfoMirrorDungeonResult
{
    public StartBuffInfo startBuffInfo = new();
}

public sealed class EnableStartBuffMirrorDungeonResult
{
    public StartBuffInfo startBuffInfo = new();
    public long cost;
    public Slinfo starlightInfo = new();
}

// Shared result shape for endpoints that return just the mutated currentInfo (a prior
// session added then removed this exact type - re-added for DetectMirrorDungeonEgogiftByStarlight).
public sealed class MirrorDungeonCurrentInfoResult
{
    public CurrentInfo currentInfo = new();
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
    // Only present when entering a shop node (e==10) - omitted on the other 66/77 captured
    // records (all other node types have no shop key at all).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ShopInfo? shopInfo;
    public List<AcquiredEgogifts> egogifts = new();
    public List<PrevUnitInfo> prevdul = new();
    public List<long> preves = new();
    public long nr;
    public List<object> cels = new();
    public long cost;
    public bool changedHiddenNode;
}

public sealed class ExitMirrorDungeonMapNodeResult
{
    public CurrentInfo currentInfo = new();
    public List<object> abnormalityLogs = new();
}

// One abnormality-unit part's rolled resistance/attack permutation (RNG - masked in the
// replay). `id` (the abnormalityPartList entry) is static-data-derived and byte-verified.
public sealed class AbnormalityLogPart
{
    public long id;
    public List<long> atrr = new();
    public List<long> atkr = new();
}

// One requested abnormality's battle log. `id` is byte-verified (echoes the request);
// k/s/p are RNG (per-battle rolls / random permutations) - masked in the replay.
public sealed class AbnormalityLogEntry
{
    public long id;
    public long k;
    public List<long> s = new();
    public List<long> p = new();
    public List<AbnormalityLogPart> ps = new();
}

public sealed class EnterMirrordungeonMapNodeBattleAfterChoiceResult
{
    public List<AbnormalityLogEntry> abnormalityLogs = new();
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
    // Echo of save.currentInfo.slinfo (the run's starlight state) - the capture returns it
    // last on every combine record, unchanged by the combine itself.
    public Slinfo starlightInfo = new();
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
