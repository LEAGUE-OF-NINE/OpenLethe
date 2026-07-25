// Types used by more than one endpoint.

public class AbnormalityUnlockInformationFormat
{
    public int id;
    public int k;
    public List<int> s;
    public List<int> p;
    public List<PartResistFormat> ps;
}

public class AccountInfoFormat
{
    public ulong uid;
    public string google_account;
    public string apple_account;
    public string steam_account;
    public string unlink_date;
    public DateUtil _unlink_date;
    public DateUtil Unlink_date;
}

public enum BOSS_RAID_DIFFICULTY { NONE, NORMAL, HARD }

public class BattlePassParameterFormat
{
    public int enemyKillCount;
    public int abnormalityKillCount;
    public bool isUsedDailyChar;
    public bool isUsedSeasonEgo;
    public bool isUsedSeasonAnnouncer;
}

public class BossRaidBuffFormat
{
    public int id;
    public int cs;
    public int ct;
    public int ns;
    public int nt;
}

public class BossRaidEgoStockFormat
{
    public string t;
    public int n;
}

public class BossRaidEnemyDataFormat
{
    public int lastWave;
    public int lastTurn;
    public int lastPhase;
    public List<BossRaidEnemyForEachFormat> enemies;
}

public class BossRaidEnemyForBSFormat
{
    public int section;
    public bool isActive;
}

public class BossRaidEnemyForEachFormat
{
    public int id;
    public long hp;
    public long maxHp;
    public int sp;
    public int mp;
    public List<BossRaidEnemyForPartFormat> partsData;
    public int lastPhase;
    public int isMain;
    public List<BossRaidBuffFormat> buf;
    public List<BossRaidShieldFormat> shield;
}

public class BossRaidEnemyForPartFormat
{
    public int id;
    public long partHp;
    public List<BossRaidEnemyForBSFormat> bs;
    public List<BossRaidEnemyForBSFormat> originBs;
    public List<BossRaidBuffFormat> buf;
    public List<BossRaidShieldFormat> shield;
}

public class BossRaidLogDataFormat
{
    public int idx;
    public int raidId;
    public int difficulty;
    public int clearturn;
    public List<BossRaidPartyDetailFormat> partydetails;
    public DateUtil date;
    public DateUtil startdate;
    public BOSS_RAID_DIFFICULTY Difficulty;
}

public class BossRaidPartyDetailFormat
{
    public int idx;
    public int turn;
    public List<BossRaidPartyPersonalityLogFormat> personalities;
    public List<BossRaidStatisticDataFormat> statistics;
}

public class BossRaidPartyPersonalityLogFormat
{
    public int pid;
    public int pord;
    public int lv;
    public int g;
}

public class BossRaidSaveDataFormat
{
    public int raidId;
    public int difficulty;
    public int state;
    public int currentIdx;
    public bool isClear;
    public List<int> personalities;
    public List<BossRaidEgoStockFormat> egostocks;
    public BossRaidEnemyDataFormat enemy;
    public string startdate;
    public bool IsEntered;
}

public class BossRaidShieldFormat
{
    public int a;
    public bool v;
    public int m;
}

public class BossRaidStatisticDataFormat
{
    public int id;
    public int gd;
    public int rd;
}

public class ChoiceEventLogFormat
{
    public int eventId;
    public int choiceIdx;
    public int count;
}

public class ContinueHistoryFormat
{
    public int paymentId;
    public List<ItemFormat> payments;
    public int turn;
}

public class ContinueUsageLogFormat
{
    public int ver;
    public string clientVer;
    public List<ContinueHistoryFormat> continueHistories;
}

public class DailyLoginRewardStateFormat
{
    public int weekid;
    public int id;
}

public class DanteAbilityUsedLogFormat
{
    public int abilityId;
    public int count;
    public int AbilityId;
    public int Count;
}

public class DateUtil
{
    public DateTime _date;
    public DateTime UtcDate;
    public bool isPast;
    public bool isFuture;
}

public class DungeonChoiceEventSaveDataFormat
{
    public List<int> sl;
    public int cs;
    public int ri;
}

public class DungeonEgoFormat
{
    public int id;
    public int g;
    public int idx;
    public int Id;
    public int SyncLevel;
    public int Index;
}

public class DungeonEgoSkillStockFormat
{
    public string t;
    public int n;
}

public class DungeonMapEgoGiftFormat
{
    public int id;
    public List<int> pids;
    public int un;
    public int ul;
    public int sn;
}

public class DungeonMapNodeFormat
{
    public int f;
    public int s;
    public int nid;
}

public class DungeonStatisticsDataFormat
{
    public int id;
    public int gd;
    public int rd;
}

public enum ELEMENT_TYPE { ITEM, EXP, CHARACTER, PERSONALITY, EGO, STAMINA, BATTLEPASS_POINT, VENDING_MACHINE, ANNOUNCER, EGO_GIFT, GACHA, USERBANNER, VENDING_MACHINE_PERSONALITY, VENDING_MACHINE_CHARACTER, SEASONAL_R_BOX, SEASONAL_O_BOX, SEASONAL_PIECE, SEASONAL_GLOBAL_GROWTH_PIECE, EVENT_ITEM, USER_TICKET_DECO_LEFT, USER_TICKET_DECO_RIGHT, USER_TICKET_DECO_EGOBG, USER_TICKET_DECO_FOR_UI, MIRRORDUNGEON_COST, UNLOCK_CODE, CHANCE, USERBANNER_RECORD, PERSONALITY_SKIN, NONE }

public class EgogiftAbilityInfoFormat
{
    public int enemyleveladder;
}

public class Element
{
    public string type;
    public ELEMENT_TYPE _type;
    public int id;
    public int num;
    public string[] tags;
    public ELEMENT_TYPE Type;
    public int Id;
    public int Num;
    public string[] Tags;
}

public class ExpDungeonClearInfoFormat
{
    public uint dungeonid;
    public uint clearnumber;
    public uint Dungeonid;
    public uint Clearnumber;
}

public class GachaLogDetail
{
    public string type;
    public ELEMENT_TYPE _type;
    public int id;
    public Element ex;
    public Element _origin;
    public Element sl;
    public ELEMENT_TYPE Type;
    public int Id;
}

public class ItemFormat
{
    public int item_id;
    public int num;
}

public class LessonProgress
{
    public int lessonId;
    public int currentLevel;
    public int experience;
    public int totalExp;
}

public enum MISSION_CONDITION_TYPE { NONE, KILL_ENEMY_ONCE_BATTLE, CLEAR_ALIVE_ALL, CLEAR_EQUANNIMITY_ALL, KILL_ENEMY_ONCE_ATTACK_SKILL, TL_DAMAGE_ATTACK_SKILL, MISSION_ALL_CLEAR, KILL_ENEMY_COUNT_BY_ATTACK_SKILL, CLEAR, CLEAR_WITHIN_TURN, CLEAR_WITHOUT_KILLED_ENEMY_BY_ENEMY, ENEMY_KILLED_BY_ENEMY, ENEMY_KILLED_BY_ENEMY_WITHIN_ONE_TURN, ENEMY_NEGATIVE_SINBUFFED, ENEMY_KILLED_BY_NEGAIVE_SINBUFF, ENEMY_KILLED_BY_PLAYER_WITH_BUFF, ENEMY_SKILL_NOTUSED, ENEMY_DAMAGED_COUNT_BY_NEGATIVE_SINBUFF, ENEMY_KILLED_BY_ONE_SKILL_HP_RATIO_80, ENEMY_NEGATIVE_BUFFED_ONE_TURN, ENEMY_MULTIKILLED_ONE_TURN, ENEMY_KILLED_BY_PARTICIPANT_ORDER, ENEMY_KILLED_ALL, CRITICAL_DMG_ONCE, ENEMY_KILLED_HAS_BUFF_STACK, CLEAR_WITHOUT_PLAYER_BUFF_STACK_REACHED, ENEMY_MULTIKILLED_ONE_SKILL, NO_TAKEN_DAMAGE_FOR_ALLY, WALPU6_MISSION2, WALPU6_MISSION3, WALPU6_MISSION4, WALPU6_MISSION5, ALLY_USING_MINUS_COIN_SKILL, GIVE_CHARGE_STACK_FOR_ALLY, GIVE_BURST_SINKING_STACK_FOR_ALLY, CLEAR_WITHOUT_PLAYER_BUFF_STACK_SUM_REACHED, CRITICAL_PLAYER_NUMBER_FIRST_ROUND, MAKE_PLAYER_PARRYING_POWER_MAXIMUM, ON_ACTIVE_EGOGIFT_9154, MAX_RESONANCE_TYPE_IN_ROUND, MAKE_PLAYER_SPEED_MAXIMUM, KILL_ENEMY_COUNT_NOT_GREATER_THAN_ON_BATTLE, WALPU8_MISSION_NOT_RETREAT, WALPU8_MISSION_WIN_DUEL, WALPU8_MISSION_GET_BUFF, WALPU8_MISSION_GET_BUFF_ALL, WALPU8_MISSION_NOT_USE_BUFF, WALPU8_MISSION_KILL_ON_EQUIP_SKILL, KILL_MIRROR_BOSS_ON_DEADLY_EDGE, KILL_ENEMY_ALL_ON_ROUND, PART_BROKEN_COUNT_ONCE_ATTACK_SKILL, ENEMY_NEGATIVE_BUFF_MAX_COUNT, ON_ACTIVE_EGOGIFT_9208, BRING_ME_ALCOHOL, RING_FINGER_MAKING_ART, ATTACK_DMG_PER_ONE_SKILL, GIVE_BUFF_STACK_FOR_ENEMY, GIVE_BUFF_STACK_FOR_ALLY, KILL_ENEMY_NOT_LESS_HP_WITHIN_FIRST_TURN, ERODE_AT_THE_SAME_TIME_FOR_ALLY, ENEMY_NEGATIVE_BUFFED_ONE_TURN_FULL_VER, TEAM_KILL_FOR_ALLY, USE_PERSONALITY_ATKTYPE, USE_PERSONALITY_SKILL, USE_PERSONALITY_DEFENSE_SKILL, USE_EGO_SKILL, USE_FRIEND_SUPPORT, CHECK_BATTLE_PASS, CHECK_PASSIVE_SKILL, STAGE_CLEAR, ACQUIRE_STAGE_PROGRESS_REWARD, PERSONALITY_LEVEL_UP, PERSONALITY_GACKSUNG, EGO_GACKSUNG, USER_LEVEL_UP, ACQUIRE_PERSONALITY, ACQUIRE_EGO, GACHA, BATTLE_PASS_MISSION_CLEAR, PURCHASE_ENKEPHALINE_MODULE, CONSUME_ENKEPHALINE, UPDATE_USER_PROFILE, LUXCAVATION_DUNGEON_CLEAR, LUXCAVATION_DUNGEON_SKIP, EXP_DUNGEON_CLEAR, EXP_DUNGEON_SKIP, THREAD_DUNGEON_CLEAR, THREAD_DUNGEON_SKIP, MIRROR_DUNGEON_ENTER, MIRROR_DUNGEON_CLEAR_NODE, MIRROR_DUNGEON_CLEAR, RAILWAY_DUNGEON_ENTER, RAILWAY_DUNGEON_CLEAR_NODE, RAILWAY_DUNGEON_CLEAR, USE_ITEM, UPDATE_FORMATION, EXCHANGE_TWINE, VENDING_MACHINE, NEW_USER, RETURN_USER, BATTLE_PASS_LEVEL_UP, MIRROR_DUNGEON_CLEAR_WITH_EGOGIFT, MIRROR_DUNGEON_CLEAR_WITH_KEYWORD_EGOGIFT, MIRROR_DUNGEON_CLEAR_WITH_TIER_EGOGIFT, MIRROR_DUNGEON_CLEAR_WITH_COMBINE_EGOGIFT, MIRROR_DUNGEON_EGOGIFT_COLLECTION, MIRROR_DUNGEON_THEME_COLLECTION, MIRROR_DUNGEON_FLOOR_CLEAR, MIRROR_DUNGEON_CLEAR_WITH_SKILL_KEYWORD, MIRROR_DUNGEON_CLEAR_WITH_UNIT_KEYWORD, MIRROR_DUNGEON_CLEAR_WITH_SHOP_REFRESH, MIRROR_DUNGEON_CLEAR_WITH_SHOP_BUY_EGOGIFT, MIRROR_DUNGEON_CLEAR_WITH_SHOP_UPGRADE_EGOGIFT, MIRROR_DUNGEON_CLEAR_WITH_SHOP_COST, MIRROR_DUNGEON_CLEAR_WITH_SHOP_UPGRADE_EGOGIFT_MAX, MIRROR_DUNGEON_CLEAR_WITH_SHOP_UPGRADE_SKILL, MIRROR_DUNGEON_WIN_BATTLE_WITH_ONE_SURVIVOR, MIRROR_DUNGEON_CLEAR_WITH_SPECIFIC_COMBINE_EGOGIFT, MIRROR_DUNGEON_CLEAR_WITH_RAILWAY_THEME, MIRROR_DUNGEON_CLEAR_WITH_STORY_THEME, MIRROR_DUNGEON_CLEAR_WITH_UNIT_KEYWORD_T_YURODIVY_UNION, THEME_CLEAR_WITH_PARTICIPATION, MIRROR_DUNGEON_CLEAR_WITH_RENTAL_FORMATION, MIRROR_DUNGEON_CLEAR_WITH_SHOP_UPGRADE_SKILL_MAX, MIRROR_DUNGEON_CLEAR_WITH_SHOP_COMBINE, MIRROR_DUNGEON_CLEAR_WITH_LAST_SHOP_COST_ZERO, MIRROR_DUNGEON_CLEAR_WITH_ALL_LEVEL, CULTIVATION_TEST_CLEAR, CULTIVATION_ALL_CLASS_PASSED, CULTIVATION_MAX_CONDITION_DAYS, CULTIVATION_END, CULTIVATION_CLASS_PASSED_OF, CULTIVATION_STAT, CULTIVATION_COMBO, MIRROR_DUNGEON_CLEAR_WITH_CONSTRAINT, MIRROR_DUNGEON_CLEAR_CONSTRAINT_TOTAL_SCORE, MIRROR_DUNGEON_CLEAR_HIDDEN_BATTLE, MIRROR_DUNGEON_ACQUIRE_SPECIFIC_EGOGIFT, MIRROR_DUNGEON_CLEAR_WITHOUT_9083GIFT, THEME_CLEAR_WITH_PARTICIPATION_SPECIAL_FOR_SPIDERS, MIRROR_DUNGEON_CLEAR_DETECTING_SKILL, THEME_CLEAR_WITH_PARTICIPATION_SPECIAL_FOR_BLOOD, BOSS_RAID_CLEAR }

public class MirrorDungeonClearedFloorFormat
{
    public int floor;
    public int difficulty;
    public int Floor;
    public int Difficulty;
}

public class MirrorDungeonConstraintInfoFormat
{
    public int flooridx;
    public Il2CppStructArray<int> ids;
}

public class MirrorDungeonCurrentInfoFormat
{
    public int eid;
    public List<MirrorDungeonSaveUnitInfoFormat> dul;
    public int sepsId;
    public List<MirrorDungeonEgoGiftPoolSetFormat> seps;
    public int sepsCreated;
    public List<RandomDungeonMapThemeFormat> tfs;
    public Il2CppReferenceArray<ThemeFloorPool> tfps;
    public int tfpsCreated;
    public Il2CppStructArray<int> peids;
    public Il2CppStructArray<int> phbids;
    public List<RandomDungeonEncounterRewardEventInfoFormat> rre;
    public int ri;
    public UserMirrorDungeonShopDataFormat_NEW shop;
    public int cost;
    public int usedcost;
    public List<MirrorDungeonPrevUnitInfoFormat> prevdul;
    public List<int> preves;
    public int etype;
    public Il2CppStructArray<int> upids;
    public List<int> leveladders;
    public string startKeyword;
    public Il2CppStructArray<int> spid;
    public int startBufPoint;
    public Il2CppReferenceArray<MirrorDungeonClearedFloorFormat> cfs;
    public MirrorDungeonExtraFlagFormat efs;
    public MirrorDungeonMissionsFormat missions;
    public StarlightInfoFormat slinfo;
    public int rentalid;
    public Il2CppReferenceArray<MirrorDungeonConstraintInfoFormat> scinfos;
    public EgogiftAbilityInfoFormat egabilityinfo;
    public int isegr;
    public DungeonMapNodeFormat cn;
    public List<DungeonMapEgoGiftFormat> egs;
    public List<int> pnids;
    public int nr;
    public List<DungeonChoiceEventSaveDataFormat> pce;
    public List<DungeonEgoSkillStockFormat> ess;
    public int dn;
    public Il2CppReferenceArray<ChoiceEventLogFormat> cels;
}

public class MirrorDungeonEgoGiftPoolSetFormat
{
    public int setId;
    public string keyword;
    public List<int> pool;
}

public class MirrorDungeonExtraFlagFormat
{
    public int rpf;
    public int csnft;
    public int sbmlos;
    public int egmlos;
    public int snft;
    public bool RevivedPerFloor;
    public bool isCurrentNewthemeFloor;
}

public class MirrorDungeonFormationEgoFormat
{
    public int prevEgoId;
    public int nextEgoId;
}

public class MirrorDungeonFormationFormat
{
    public int pervPersonalityId;
    public int nextPersonalityId;
    public List<MirrorDungeonFormationEgoFormat> egos;
    public int skinId;
}

public class MirrorDungeonGetCharacterInfoFormat
{
    public int pid;
    public List<DungeonEgoFormat> egos;
}

public class MirrorDungeonHistoryFormat
{
    public int dungeonid;
    public Il2CppReferenceArray<MirrorDungeonPersonalityRestStatusFormat> restStatuses;
    public MirrorDungeonPrevPlayRecordFormat prevPlayRecord;
    public int dungeonId;
}

public class MirrorDungeonMissionsFormat
{
    public int sr;
    public int sbe;
    public Il2CppStructArray<int> smuet;
    public int sc;
    public int sec;
    public int nlm;
    public int sfs;
    public int sfb;
    public int ds;
}

public class MirrorDungeonPersonalityRestStatusFormat
{
    public int pid;
    public int cnt;
}

public class MirrorDungeonPrevPlayRecordFormat
{
    public Il2CppStructArray<int> pids;
    public int epsId;
    public Il2CppStructArray<int> prevtfids;
}

public class MirrorDungeonPrevUnitInfoFormat
{
    public int pid;
    public Il2CppStructArray<int> upidx;
}

public class MirrorDungeonSaveInfoFormat
{
    public int dungeonId;
    public int idx;
    public MirrorDungeonCurrentInfoFormat currentInfo;
    public RandomDungeonMapFormat dungeonMap;
    public int isEndDungeon;
    public int isReset;
    public List<int> choiceEventList;
    public List<DungeonStatisticsDataFormat> statistics;
    public List<int> encounterstatistics;
    public int version;
    public string startdate;
}

public class MirrorDungeonSaveUnitInfoFormat
{
    public List<int> upidx;
    public int mlos;
    public int pid;
    public int ch;
    public int cm;
    public int mhos;
    public int g;
    public int l;
    public List<DungeonEgoFormat> es;
    public int isp;
    public int sid;
}

public class MirrorDungeonStartBuffInfoFormat
{
    public int dungeonid;
    public List<int> bufstate;
    public List<int> enabled;
}

public class MissionConditionContextFormat
{
    public MISSION_CONDITION_TYPE type;
    public int target1;
    public int target2;
    public int target3;
    public int value;
}

public enum PERSONALITY_ILLUST_STATE { BEFORE_GACKSUNG, AFTER_GACKSUNG, NONE }

public class PartResistFormat
{
    public int id;
    public List<int> atrr;
    public List<int> atkr;
}

public class ProfileEgoContainIndexFormat
{
    public int idx;
    public int id;
    public int g;
}

public class ProjectGSFinalStats
{
    public long uid;
    public int playCount;
    public List<ProjectGSStats> stats;
    public List<LessonProgress> lessonProgress;
}

public class ProjectGSGameData
{
    public long uid;
    public ProjectGSGameState gameState;
    public int currentDay;
    public int playCount;
    public List<ProjectGSStats> stats;
    public List<LessonProgress> lessonProgress;
}

public enum ProjectGSGameState { NONE, BEFORE_LESSON, IN_LESSON, AFTER_LESSON, END }

public class ProjectGSStats
{
    public int wisdom;
    public int courage;
    public int benevolence;
    public int condition;
    public int perfectDays;
}

public enum RAILWAY_BATTLE_STATE_TYPE { NONE, MRR6_SPECIFIC }

public class RailwayBattleStateByNodeFormat
{
    public int nid;
    public List<RailwayBattleStateFormat> states;
}

public class RailwayBattleStateFormat
{
    public RAILWAY_BATTLE_STATE_TYPE type;
    public string state;
}

public class RailwayBuffFormat
{
    public int id;
    public int count;
    public List<int> targetids;
}

public class RailwayBuffSetFormat
{
    public int setid;
    public List<RailwayBuffFormat> buffs;
    public int recentbuffid;
    public List<int> currentbuffids;
}

public class RailwayBuffSetInNodeFormat
{
    public int nid;
    public List<RailwayBuffWithEgogiftsFormat> buffs;
    public int NodeId;
}

public class RailwayBuffWithEgogiftsFormat
{
    public int buffId;
    public int playeregogift;
    public int enemyegogift;
}

public class RailwayDetailStatisticsDataFormat
{
    public int collectionId;
    public List<RailwayUnitInfoFormat> personalities;
    public List<RailwayStatisticsDataFormat> statistics;
}

public class RailwayDungeonSaveInfoFormat
{
    public int id;
    public int prevclearnode;
    public int currentnode;
    public int lastclearnode;
    public List<RailwayUnitInfoFormat> personalities;
    public int payreward;
    public int rewardstate;
    public List<RailwayExtraRewardStateFormat> extrarewardstate;
    public DateUtil firstcleardate;
    public int currentclearrotation;
    public int lastenternodeid;
    public int lastclearrotation;
    public Il2CppReferenceArray<RailwayBuffSetFormat> buffsets;
    public Il2CppReferenceArray<RailwayBuffSetInNodeFormat> buffsetsbyegogift;
    public int initseed;
    public int currentseed;
    public bool IsPayReward;
}

public class RailwayEGOStockFormat
{
    public string t;
    public int n;
}

public class RailwayExtraRewardStateFormat
{
    public int id;
    public bool isRewarded;
}

public class RailwayLogDataFormat
{
    public int idx;
    public List<RailwayUnitInfoFormat> personalities;
    public List<RailwayStatisticsDataFormat> statistics;
    public List<RailwayDetailStatisticsDataFormat> detailstatistics;
    public int clearturn;
    public List<RailwayTurnsPerNode> turnspernode;
    public int clearrotation;
    public Il2CppReferenceArray<RailwayBuffSetFormat> buffsets;
    public Il2CppReferenceArray<RailwayBuffSetInNodeFormat> buffsetsbyegogift;
    public string date;
    public int deadunitnumber;
    public List<RailwayBattleStateByNodeFormat> battleStatesPerNode;
}

public class RailwayNodeDataFormat
{
    public int nodeid;
    public List<RailwayEGOStockFormat> egostocks;
    public List<RailwayUnitStatusFormat> status;
    public int clearturn;
    public int playturn;
    public List<RailwayStatisticsDataFormat> statistics;
    public SaveDataForRailwayDungeon enemy;
    public uint nodestate;
    public List<RailwayBattleStateFormat> battleStates;
}

public class RailwayStatisticsDataFormat
{
    public int id;
    public int gd;
    public int rd;
    public int Id;
    public int GiveDamage;
    public int ReceiveDamage;
}

public class RailwayTurnsPerNode
{
    public int nid;
    public int turn;
}

public class RailwayUnitInfoFormat
{
    public int pid;
    public int g;
    public int l;
    public List<DungeonEgoFormat> es;
    public int sp;
    public int gi;
    public int pord;
    public int sid;
    public int Pid;
    public int SyncLevel;
    public int Level;
    public List<DungeonEgoFormat> Egos;
    public bool IsFriendSupport;
    public int PersonalityOrder;
    public int IntIllustState;
    public PERSONALITY_ILLUST_STATE IllustState;
    public bool IsGacksungIllust;
    public int SkinId;
}

public class RailwayUnitSinFormat
{
    public Il2CppStructArray<int> sp;
    public Il2CppStructArray<int> cs;
    public int rs;
}

public class RailwayUnitStatusFormat
{
    public int pid;
    public int hp;
    public int mp;
    public int isp;
    public RailwayUnitSinFormat sin;
    public Il2CppReferenceArray<DungeonEgoFormat> egos;
    public int sp;
    public int lv;
    public int g;
    public int gi;
    public int sid;
    public int pord;
    public int Hp;
    public bool IsParticipated;
    public RailwayUnitSinFormat Sin;
    public Il2CppReferenceArray<DungeonEgoFormat> Egos;
    public int Sp;
    public int Lv;
    public int G;
    public int Gi;
    public int Sid;
    public int ParticipationOrder;
}

public class RandomDungeonEncounterRewardEventInfoFormat
{
    public string rt;
    public int se;
    public int sh;
    public List<int> pool;
    public List<int> pool_v2;
    public List<int> pool_v3;
}

public class RandomDungeonMapFormat
{
    public List<RandomDungeonMapNodeFormatForMapFormat> ns;
}

public class RandomDungeonMapNodeFormatForMapFormat
{
    public int f;
    public int s;
    public int nid;
    public int e;
    public int eid;
    public List<int> nnids;
}

public class RandomDungeonMapThemeFormat
{
    public int f;
    public int tid;
    public int idx;
    public int tfid;
    public List<int> egs;
    public List<int> upegs;
}

public class SaveDataForBS
{
    public int section;
    public bool isActive;
}

public class SaveDataForEach
{
    public int abnormalityHp;
    public int abnormalityMaxHp;
    public int abnormalityMp;
    public List<SaveDataForPart> partsData;
    public int lastPhase;
    public List<bool> checkList;
}

public class SaveDataForPart
{
    public int partHp;
    public List<SaveDataForBS> bs;
    public List<SaveDataForBS> originBs;
}

public class SaveDataForRailwayDungeon
{
    public int lastWave;
    public int lastTurn;
    public List<SaveDataForEach> abnoSaveDataList;
}

public class StagePersonalityInfoFormat
{
    public int personalityid;
    public int prevlevel;
    public int totaladdexp;
}

public class StarlightInfoFormat
{
    public int pslp;
    public int rsbp;
    public int rucc;
    public List<int> dpp;
    public List<int> brcp;
    public int pfb;
    public int scc;
    public int ieedt;
    public List<int> degids;
    public int segr;
    public int ds;
}

public class StoryDungeonCurrentInfoFormat
{
    public List<StoryDungeonSaveUnitInfoFormat> dul;
    public DungeonMapNodeFormat scpn;
    public List<DungeonMapEgoGiftFormat> scpegl;
    public List<int> opn;
    public bool cu;
    public DungeonMapNodeFormat cn;
    public List<DungeonMapEgoGiftFormat> egs;
    public List<int> pnids;
    public int nr;
    public List<DungeonChoiceEventSaveDataFormat> pce;
    public List<DungeonEgoSkillStockFormat> ess;
    public int dn;
    public Il2CppReferenceArray<ChoiceEventLogFormat> cels;
}

public class StoryDungeonSaveInfoFormat
{
    public int dungeonid;
    public StoryDungeonCurrentInfoFormat currentinfo;
}

public class StoryDungeonSaveUnitInfoFormat
{
    public int sp;
    public int gi;
    public int order;
    public int pid;
    public int ch;
    public int cm;
    public int mhos;
    public int g;
    public int l;
    public List<DungeonEgoFormat> es;
    public int isp;
    public int sid;
}

public class StoryMirrorDungeonCurrentInfoFormat
{
    public DungeonMapNodeFormat cn;
    public List<DungeonMapEgoGiftFormat> egs;
    public List<int> pnids;
    public int nr;
    public List<DungeonChoiceEventSaveDataFormat> pce;
    public List<DungeonEgoSkillStockFormat> ess;
    public int eid;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> dul;
    public List<RandomDungeonEncounterRewardEventInfoFormat> rre;
    public UserStoryMirrorDungeonShopDataFormat shop;
    public int cost;
    public List<MirrorDungeonPrevUnitInfoFormat> prevdul;
    public List<int> preves;
    public List<MirrorDungeonEgoGiftPoolSetFormat> seps;
    public int sepsCreated;
}

public class StoryMirrorDungeonSaveInfoFormat
{
    public int dungeonid;
    public StoryMirrorDungeonCurrentInfoFormat currentinfo;
    public RandomDungeonMapFormat map;
    public List<int> choiceeventlist;
    public List<DungeonStatisticsDataFormat> statistics;
}

public class StoryMirrorDungeonSaveUnitInfoFormat
{
    public int sp;
    public List<int> upidx;
    public int mlos;
    public int pid;
    public int ch;
    public int cm;
    public int mhos;
    public int g;
    public int l;
    public List<DungeonEgoFormat> es;
    public int isp;
    public int sid;
}

public class SupportPersonalitySlotFormat
{
    public int idx;
    public int pid;
    public int l;
    public List<ProfileEgoContainIndexFormat> egos;
    public int gl;
    public int gi;
    public int sid;
}

public class ThemeFloorPool
{
    public int idx;
    public int tfid;
    public Il2CppStructArray<int> egs;
    public Il2CppStructArray<int> upegs;
}

public class ThreadDungeonClearInfoFormat
{
    public uint dungeonid;
    public uint clearnumber;
    public uint dungeonlevel;
    public uint Dungeonid;
    public uint Clearnumber;
    public uint Dungeonlevel;
}

public class UserAuthFormat
{
    public long uid;
    public long public_id;
    public int db_id;
    public string auth_code;
    public string last_login_date;
    public string last_update_date;
    public int data_version;
}

public class UserBannerDataFormat
{
    public int id;
    public string acquiretime;
    public int value;
    public int value2;
    public int value3;
    public int value4;
    public int value5;
}

public class UserMirrorDungeonShopDataFormat_NEW
{
    public Il2CppReferenceArray<UserMirrorDungeonShopSlotFormat_NEW> slots;
    public int rc;
    public int fre;
    public int fkre;
    public int cf;
    public int aec;
    public int aesp;
}

public class UserMirrorDungeonShopSlotFormat_NEW
{
    public string t;
    public int id;
    public int s;
    public bool IsSoldOut;
}

public class UserProfileBorderFormat
{
    public int id;
    public string date;
}

public class UserProfileEgobackgroundFormat
{
    public int id;
    public string date;
}

public class UserPublicBannerFormat
{
    public int id;
    public int value;
    public int value2;
    public int value3;
    public int value4;
    public int value5;
    public int idx;
}

public class UserPublicProfileFormat
{
    public string public_uid;
    public int illust_id;
    public int illust_gacksung_level;
    public int leftborder_id;
    public int rightborder_id;
    public int egobackground_id;
    public int sentence_id;
    public int word_id;
    public List<UserPublicBannerFormat> banners;
    public int level;
    public string date;
}

public class UserStageStatisticEgoFormat
{
    public int id;
    public int gacksung;
    public int Id;
    public int Gacksung;
}

public class UserStageStatisticPersonalityFormat
{
    public int id;
    public int level;
    public int gacksung;
    public int order;
    public int isrental;
    public List<UserStageStatisticEgoFormat> egos;
    public int isdead;
    public int skinId;
    public int Id;
    public int Level;
    public int Gacksung;
    public int Order;
    public int IsRental;
    public List<UserStageStatisticEgoFormat> Egos;
    public int IsDead;
    public int SkinId;
}

public class UserStoryMirrorDungeonShopDataFormat
{
    public int ph;
    public int pup;
    public int upid;
    public Il2CppStructArray<int> peg;
    public int pcf;
    public Il2CppStructArray<int> egpool;
    public int rc;
    public int fre;
}

public class UserTheaterInfoFormat
{
    public List<string> rewardedIDList;
}

