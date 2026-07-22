using System.Collections.Generic;

namespace OpenLethe.Server.Wire;

// Server-authored ports of the Rust story-mirror-dungeon structs (models/src/types.rs).
// Field names match Rust serde exactly. No [JsonIgnore]; lists init empty. Shared types
// (Currentnode, AcquiredEgogifts, ChoiceEventData, EgoSkillStock, RemainRewardEvent,
// PrevUnitInfo, StartEgoGiftPoolSets, DungeonMap, Ns, MdStatistics) are reused from
// Wire/MirrorDungeon.cs; Egos from Wire/Railway.cs.

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
