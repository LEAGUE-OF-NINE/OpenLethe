using System;
using System.Collections.Generic;
using System.Linq;
using OpenLethe.Server.MirrorDungeon.Model;

namespace OpenLethe.Server;

// Mirror Dungeon battle-victory cost. The payout the client shows on a map node is the generic
// table in mirror-dungeon-common-data: originbattlevictorycostvalueList (normal difficulty) /
// originbattlevictorycostvalueListHardLevel (hard), one base cost per encounter type, times that
// type's weight in the floor's battleCostWeightsByFloor row. Retail (normal / hard):
//   battle 75 / 85, abno 100 / 120, hard battle 120 / 150, hard abno 150 / 180,
//   boss 200 / 250 x {1, 1.2, 1.5, 2, 2} for floors 1-5 (6+ keep the floor-5 row).
// The per-stage mirrordungeon-acquire-cost-in-battle table is deprecated and no longer read: its
// amounts did not match what the client shows, and its boss stages past floor 4 paid nothing.
public static class MdCost
{
    // Map node kind (e) -> the common table's encounterType.
    private static string? EncounterType(long nodeE) => nodeE switch
    {
        1 => "BATTLE",
        5 => "AB_BATTLE",
        2 => "HARD_BATTLE",
        14 => "HARD_AB_BATTLE",
        6 => "BOSS",
        MdMapGen.HiddenBattleNodeType => "HIDDEN_BATTLE",
        _ => null,
    };

    /// The cost for winning a node of kind `nodeE` on the 0-based floor `floor`, before the
    /// cost gifts (ApplyCostGifts). 0 for a node that is not a battle.
    public static long BattleVictoryCost(long nodeE, long floor, bool hard)
    {
        var type = EncounterType(nodeE);
        if (type is null) return 0;
        var entry = MdCommonTable.VictoryCosts(hard).FirstOrDefault(c => c.encounterType == type);
        if (entry is null) return 0;

        // The floor's weight row; past the last listed floor the last row keeps applying.
        var row = MdCommonTable.CostWeightsByFloor
            .Where(w => w.floor <= floor)
            .OrderByDescending(w => w.floor)
            .FirstOrDefault();
        var weight = row is not null && entry.battleCostWeightsByFloorIdx >= 0
            && entry.battleCostWeightsByFloorIdx < row.weights.Count
            ? row.weights[entry.battleCostWeightsByFloorIdx]
            : 1.0;
        return (long)Math.Round(entry.cost * weight, MidpointRounding.AwayFromZero);
    }

    // Story Mirror Dungeon only (port of lethe-server md_cost.rs CostManager::get_default_cost);
    // the Mirror Dungeon itself pays BattleVictoryCost.
    private static readonly long[] BossFloorCosts = { 200, 240, 300, 400 };

    public static long GetDefaultCost(long nodeE, long floor) => nodeE switch
    {
        1 => 60,
        2 => 120,
        5 => 100,
        6 => floor is >= 0 and <= 3 ? BossFloorCosts[floor] : 0,
        14 => 150,
        _ => 0,
    };

    // Two retail gifts scale battle payouts (text from limbus_locale EN/egoGiftData.json):
    //   9079 Golden Urn   「Gain +20% Cost after winning a combat Encounter.」
    //   9242 Bongy Plush  「Multiply Cost owned by 1.5 upon clearing a Floor」
    private const long GoldenUrnGift = 9079;
    private const long BongyPlushGift = 9242;

    // Binary Star-shop's "Encounter Reward Cost +20/30/40%" is the same kind of bonus as Golden Urn's
    // +20%, so the two rates add up before Bongy Plush multiplies a boss payout.
    public static long ApplyCostGifts(Run run, long nodeE, long cost)
    {
        if (cost <= 0) return cost;
        var ratePercent = OpenLethe.Server.MirrorDungeon.Rules.StartBuffEffects.WinBattleCostRate(run);
        if (run.Gifts.Items.Any(g => g.Id == GoldenUrnGift)) ratePercent += 20;
        if (ratePercent != 0) cost = cost * (100 + ratePercent) / 100;
        if (nodeE == 6 && run.Gifts.Items.Any(g => g.Id == BongyPlushGift)) cost = cost * 3 / 2;
        return cost;
    }

    // 9080 Milepost of Survival 「Upon clearing Floor Boss: gain (80 + # of Identities participating
    // in the Encounter x 5) additional Cost (unaffected by effects that raise Cost after clearing an
    // Encounter)」 - so it is added on top of ApplyCostGifts, never scaled by it. The exit request
    // lists the whole roster, so the participants are the boss stage's deploy cap
    // (participantInfo.max), limited to the sinners still standing when the fight began.
    private const long MilepostOfSurvivalGift = 9080;

    public static long MilepostOfSurvivalBonus(Run run, long nodeE, long stageId, long livingBeforeBattle)
    {
        if (nodeE != 6 || !run.Gifts.Items.Any(g => g.Id == MilepostOfSurvivalGift)) return 0;
        var cap = OpenLethe.Server.MirrorDungeon.Data.MdAbRewards.MaxParticipants(stageId) ?? livingBeforeBattle;
        return 80 + Math.Min(cap, livingBeforeBattle) * 5;
    }
}
