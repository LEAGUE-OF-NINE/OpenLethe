using System.Collections.Generic;

namespace OpenLethe.Server.Wire;

// Server-authored ports of the Rust story-mirror-dungeon structs (models/src/types.rs).
// Field names match Rust serde exactly. No [JsonIgnore]; lists init empty. Shared types are
// reused, not redefined - EgoSkillStock, Ns, DungeonMap, StartEgoGiftPoolSets,
// RemainRewardEvent, PrevUnitInfo and MdStatistics from Wire/MirrorDungeon.cs; Currentnode,
// AcquiredEgogifts and ChoiceEventData from Wire/StoryDungeon.cs; Egos from Wire/Railway.cs.
// All live in this same OpenLethe.Server.Wire namespace.

/// Port of models/src/types.rs Dungeonunitlist2 (story MIRROR dungeon). The third unit
/// type: story-dungeon Dungeonunitlist has `gi`; MD Dungeonunitlist1 differs again. This
/// one has upidx/mlos and no gi. Do not interchange them.
public sealed class Dungeonunitlist2
{
    public long sp;
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

/// Port of models/src/types.rs UserStoryMirrorDungeonShopDataFormat. Despite the Rust
/// name's `Format` suffix this is the server's own type. It is a strict SUBSET of the MD
/// ShopInfo (no fkre/aesp/pcs), so it is NOT interchangeable with it.
public sealed class UserStoryMirrorDungeonShopData
{
    public long ph;
    public long pup;
    public long upid;
    public List<long> peg = new();
    public long pcf;
    public List<long> egpool = new();
    public long rc;
    public long fre;
}

/// Port of models/src/types.rs Currentinfo1 (story mirror dungeon).
public sealed class Currentinfo1
{
    public Currentnode cn = new();
    public List<AcquiredEgogifts> egs = new();
    public List<long> pnids = new();
    public long nr;
    public List<ChoiceEventData> pce = new();
    public List<EgoSkillStock> ess = new();
    public long eid;
    public List<Dungeonunitlist2> dul = new();
    public List<RemainRewardEvent> rre = new();
    public UserStoryMirrorDungeonShopData shop = new();
    public long cost;
    public List<PrevUnitInfo> prevdul = new();
    public List<long> preves = new();
    public List<StartEgoGiftPoolSets> seps = new();
    public long sepsCreated;
}

/// Port of models/src/types.rs StoryMirrorSaveInfo. Note `currentinfo` is all-lowercase
/// here (the MD save uses `currentInfo` with a capital I).
public sealed class StoryMirrorSaveInfo
{
    public long dungeonid;
    public Currentinfo1 currentinfo = new();
    public DungeonMap map = new();
    public List<long> choiceeventlist = new();
    public List<MdStatistics> statistics = new();
}

// ---- Task 4: request params ----

public sealed class EnterStoryMirrorDungeonParams
{
    public long dungeonid;
    public long idx;
}

public sealed class UpdateStoryMirrorDungeonParams
{
    public List<Formation> formation = new();
}

public sealed class AcquireStartEgoGiftsStoryMirrorDungeonParams
{
    public long selectedSetId;
    public List<long> selectedEgoGiftIds = new();
}

public sealed class EnterStoryMirrorDungeonMapNodeParams
{
    public Currentnode currentnode = new();
    public List<long> abnormalityids = new();
    public List<long> participatedPIds = new();
}

public sealed class UpdateStoryMirrorDungeonMapNodeParams
{
    public Currentnode currentnode = new();
    public ChoiceEventData choiceEventData = new();
    // Rust reads neither of these; the client sends them, the handler ignores them.
    public List<Dungeonunitlist2> dungeonUnitList = new();
    public List<AcquiredEgogifts> updatedEgoGifts = new();
}

/// Rust's RequestParamApiExitStoryMirrorDungeonMapNode also carries `noderesult`,
/// `choiceEventData`, `battlePassParameters`, `abnormalityLogs`, `updatedEgoGifts`,
/// `statistics` and `usedDanteAbilityCount` - none of which the handler reads, so (matching
/// this project's existing handler style, e.g. ExitMirrorDungeonMapNodeParams) only the
/// fields actually used are declared here.
public sealed class ExitStoryMirrorDungeonMapNodeParams
{
    public Currentnode currentnode = new();
    public List<Dungeonunitlist2> dungeonunitlist = new();
    public long isupdatedEgoSkillStock;
    public List<EgoSkillStock> egoSkillStockList = new();
}

// ---- Task 4: response results ----

public sealed class EnterStoryMirrorDungeonResult
{
    public StoryMirrorSaveInfo saveInfo = new();
}

public sealed class UpdateStoryMirrorDungeonResult
{
    public StoryMirrorSaveInfo saveInfo = new();
}

public sealed class AcquireStartEgoGiftsStoryMirrorDungeonResult
{
    public List<AcquiredEgogifts> egoGifts = new();
    public List<StartEgoGiftPoolSets> startEgoGiftPoolSets = new();
    public long startEgoGiftCreatedCount;
}

public sealed class EnterStoryMirrorDungeonMapNodeResult
{
    public List<object> abnormalityLogs = new();
    public List<long> passingNodeIds = new();
    public Currentnode currentNode = new();
    public UserStoryMirrorDungeonShopData shopInfo = new();
    public List<AcquiredEgogifts> egogifts = new();
    public List<PrevUnitInfo> prevdul = new();
    public List<long> preves = new();
}

public sealed class UpdateStoryMirrorDungeonMapNodeResult
{
    public List<ChoiceEventData> prevChoiceEvent = new();
    public List<AcquiredEgogifts> currentEgoGifts = new();
    public List<Dungeonunitlist2> dungeonUnitList = new();
}

public sealed class ExitStoryMirrorDungeonMapNodeResult
{
    public Currentinfo1 currentInfo = new();
    public List<object> abnormalityLogs = new();
}

// ---- cycle 5c: shop / ego-gift ----

public sealed class PurchaseEgoGiftStoryMirrorDungeonParams
{
    public long idx;
}

public sealed class SellEgoGiftStoryMirrorDungeonParams
{
    public long id;
}

public sealed class UpgradeEgoGiftStoryMirrorDungeonParams
{
    public long egoGiftId;
}

public sealed class RefreshShopEgoGiftsStoryMirrorDungeonParams
{
    // Declared because the client sends it; Rust's handler never reads it.
    public string keyword = "";
}

public sealed class PurchaseHealStoryMirrorDungeonParams
{
    public long idx;
    public long pid;
}

/// Shared by the purchase-ego-gift and sell-ego-gift responses (identical shape in Rust).
/// `egogifts` is lowercase here - that is the wire contract, not a typo.
public sealed class StoryMirrorDungeonShopEgoGiftResult
{
    public long cost;
    public List<AcquiredEgogifts> egogifts = new();
    public UserStoryMirrorDungeonShopData shopInfo = new();
    public List<Dungeonunitlist2> dungeonUnitList = new();
}

public sealed class UpgradeEgoGiftStoryMirrorDungeonResult
{
    public long cost;
    public AcquiredEgogifts egoGift = new();
    public List<Dungeonunitlist2> dungeonUnitList = new();
}

public sealed class RefreshShopEgoGiftsStoryMirrorDungeonResult
{
    public long cost;
    public UserStoryMirrorDungeonShopData shopInfo = new();
}

public sealed class PurchaseHealStoryMirrorDungeonResult
{
    public long cost;
    public List<Dungeonunitlist2> dungeonUnitList = new();
    public UserStoryMirrorDungeonShopData shopInfo = new();
}

public sealed class AcquireRewardEgoGiftsStoryMirrorDungeonParams
{
    public List<long> selectIndexList = new();
}

public sealed class AcquireRewardEgoGiftsStoryMirrorDungeonResult
{
    public List<AcquiredEgogifts> egoGifts = new();
    public List<RemainRewardEvent> remainRewardEvent = new();
    public List<Dungeonunitlist2> dungeonUnitList = new();
}

public sealed class CombineEgoGiftStoryMirrorDungeonParams
{
    public List<long> materialEgoGiftIds = new();
    // Declared because the client sends it; Rust's handler never reads it.
    public string keyword = "";
}

public sealed class CombineEgoGiftStoryMirrorDungeonResult
{
    public AcquiredEgogifts resultEgoGift = new();
    public bool isSuccess;
    public List<AcquiredEgogifts> egoGifts = new();
    public List<Dungeonunitlist2> dungeonUnitList = new();
}
