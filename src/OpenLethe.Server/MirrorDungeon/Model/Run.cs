namespace OpenLethe.Server.MirrorDungeon.Model;

// The Mirror Dungeon run aggregate. Foundation complete: every field is a readable,
// wire-independent domain property. WireMapper is the total, lossless wire<->domain map;
// MdDomainWireIndependenceTests proves no property leaks a wire type and no Raw* scaffolding
// remains.
public sealed class Run
{
    // ---- outer MirrorOriginSaveInfo fields ----
    public long DungeonId { get; set; }
    public long Idx { get; set; }
    public Floor Floor { get; set; } = new();             // was RawCn + RawDungeonMap
    public List<long> ChoiceEventList { get; set; } = new();
    public List<SinnerStat> SinnerStats { get; set; } = new();  // was RawStatistics
    public List<long> EncounterStatistics { get; set; } = new();
    public long IsEndDungeon { get; set; }
    public long IsReset { get; set; }
    public long Version { get; set; }
    public string StartDate { get; set; } = "";

    // ---- currentInfo fields ----
    public long Eid { get; set; }
    public List<PartyUnit> Party { get; set; } = new();   // was RawDul (dul[*])
    public long SepsId { get; set; }
    public List<StartEgoGiftPool> StartPools { get; set; } = new();  // was RawSeps
    public long SepsCreated { get; set; }
    public List<ThemeFloor> ThemeFloors { get; set; } = new();  // was RawTfs (tfs[*])
    public List<ThemePool> ThemePools { get; set; } = new();    // was RawTfps (tfps[*])
    public long TfpsCreated { get; set; }
    public List<RewardEvent> RewardEvents { get; set; } = new(); // was RawRre (rre[*])
    public long Ri { get; set; }
    public long Cost { get; set; }
    public long UsedCost { get; set; }
    public ShopState Shop { get; set; } = new();                    // was RawShop
    public List<PrevUnit> PrevUnits { get; set; } = new();          // was RawPrevdul
    public List<long> Preves { get; set; } = new();
    public List<long> LevelAdders { get; set; } = new();
    public string StartKeyword { get; set; } = "";
    public long StartBuffPoint { get; set; }
    public List<ConstraintScore> ConstraintScores { get; set; } = new();  // was RawCfs
    public LevelOffsets LevelOffsets { get; set; } = new(); // was RawEfs
    public EgoGiftInventory Gifts { get; set; } = new();   // was RawEgs (egs[*])
    public List<long> Pnids { get; set; } = new();
    public long Nr { get; set; }
    public List<ChoiceEvent> ChoiceEvents { get; set; } = new();    // was RawPce
    public List<SkillStock> SkillStocks { get; set; } = new();      // was RawEss
    public long Dn { get; set; }
    public List<long> Peids { get; set; } = new();
    public List<long> Phbids { get; set; } = new();
    public long Etype { get; set; }
    public List<long> Upids { get; set; } = new();
    public List<long> Spid { get; set; } = new();
    public MissionState Missions { get; set; } = new();             // was RawMissions
    public List<object> Cels { get; set; } = new();
    public StarlightState Starlight { get; set; } = new();          // was RawSlinfo
    public long RentalId { get; set; }
    public List<ConstraintSelection> ConstraintSelections { get; set; } = new(); // was RawScinfos
    public EnemyAbility EnemyAbility { get; set; } = new();         // was RawEgabilityinfo
    public long Isegr { get; set; }
}
