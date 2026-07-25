using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.MirrorDungeon.Rules;

// The full ExitMapNode lifecycle: node-exit party/skill-stock merge, egmlos/eid/pnids/peids
// bookkeeping, and the reward/rre event engine (build RewardEvents per node kind `e`, grant
// confirmed gifts, queue battle-reward cases/constraints, drive LevelOffsets). Verbatim port of
// Handlers/MirrorDungeonMap.cs ExitMirrorDungeonMapNode (:318-565) + its private helpers
// (BuildBattleRewardCase :89, EncounterCardBonus :86, HiddenGiftLevelBump/RecomputeEgmlos are
// already ported to SharedRules/Effects - Task 2 - and reused here, NOT re-ported).
// See the handler-migration Task 7 brief.
public static class RewardEngine
{
    // The MD7 start buffs that grant personality level-cap offsets (verbatim port of
    // MirrorDungeonMap.cs LevelOffsets :30 / StartRunRules.cs :20 / MapGenerator.cs :20 - same
    // fixed set, see there for why it's fixed rather than read from the per-run enabled buffs).
    private static readonly MdLevelOffsets LevelOffsets = MdStartBuffs.LevelOffsets(new long[] { 106, 102 });

    // Owned ego gift 9187 "Special Catalogue" - verbatim port of
    // Handlers/MirrorDungeonMap.cs EncounterCardBonusGift (:84).
    private const long EncounterCardBonusGift = 9187;

    // Result of ResolveNodeExit - everything ExitMirrorDungeonMapNodeResult needs beyond
    // `currentInfo` (which the handler builds from ToWire(run) after this call).
    public readonly record struct NodeExitResult(List<object> AbnormalityLogs);

    // Throwaway IDungeonEventSave that only records PushEgoGift ids - verbatim port of
    // Handlers/MirrorDungeonMap.cs GiftCollectorSave (:162-169). Domain-agnostic (no wire/Run
    // types involved), moved here unchanged.
    private sealed class GiftCollectorSave : OpenLethe.Server.IDungeonEventSave
    {
        public readonly List<long> GiftIds = new();
        public void PushEgoGift(long rewardId) => GiftIds.Add(rewardId);
        public Dictionary<long, OpenLethe.Server.UnitStats> GetUnitStats() => new();
        public void SetUnitStats(Dictionary<long, OpenLethe.Server.UnitStats> stats) { }
        public void AddCost(long cost) { }
    }

    // Verbatim port of Handlers/MirrorDungeonMap.cs EncounterCardBonus (:86-87), retargeted to Run.
    private static int EncounterCardBonus(Run run) =>
        run.Gifts.Items.Any(e => e.Id == EncounterCardBonusGift) ? 1 : 0;

    // Verbatim port of Handlers/MirrorDungeonMap.cs BuildBattleRewardCase (:89-105), retargeted
    // to the domain RewardEvent.
    private static RewardEvent BuildBattleRewardCase(long cnf, int extraChoices)
    {
        var sh = cnf <= 1 ? 2 : 3;
        var candidates = MdEncounterCard.PickRandomEncounterCards(7, includeStarlightMinMax: true);
        return new RewardEvent
        {
            Rt = "GetBattleRewardCase",
            Se = 1,
            Sh = sh,
            Pool = OpenLethe.Server.MdMapGen.ChooseMultiple(candidates, sh + extraChoices),
            PoolV2 = new(),
            PoolV3 = new(),
        };
    }

    // Verbatim port of the ExitMirrorDungeonMapNode handler body (:318-564). `node`/`noderesult`/
    // `choiceEventData` mirror the request DTO 1:1 (the last two are unused here - the original
    // handler also received but never read them). Caller (the handler) is responsible for
    // validating the exited node exists in run.Floor.Nodes before calling this - request
    // validation, not game logic (same contract as MapNodeRules.EnterNode).
    public static NodeExitResult ResolveNodeExit(
        Run run,
        Currentnode node,
        List<Dungeonunitlist1> dungeonunitlist,
        long noderesult,
        ChoiceEventData choiceEventData,
        bool isUpdatedEgoSkillStock,
        List<EgoSkillStock> egoSkillStockList,
        List<JsonNode> abnormalityLogs)
    {
        run.Floor.Current = new CurrentPosition { F = node.f, S = node.s, Nid = node.nid };

        // Merge the client-reported battle-mutated roster onto the authoritative party (same
        // per-pid merge SharedRules.MergeParty already implements for EnterMapNode/UpdateMapNode).
        var incomingParty = dungeonunitlist.Select(u => new PartyUnit
        {
            PersonalityId = u.pid, CurrentHp = u.ch, Cm = u.cm, Mhos = u.mhos,
            Gacksung = u.g, Level = u.l, Isp = u.isp, Sid = u.sid, Mlos = u.mlos,
            Pord = u.pord,
            UpgradeIndices = u.upidx,
            EgoSkills = u.es.Select(e => new EgoSkill { Id = e.id, G = e.g, Idx = e.idx }).ToList(),
        }).ToList();
        run.Party = SharedRules.MergeParty(run.Party, incomingParty);

        // Some nodes don't send egoSkillStockList (e.g. rest stops); when they do, the request
        // omits zero-count types, so merge the updated counts by type onto the existing per-type
        // slots instead of replacing (preserves slot set/order).
        if (isUpdatedEgoSkillStock)
            foreach (var upd in egoSkillStockList)
            {
                var slot = run.SkillStocks.FirstOrDefault(e => e.T == upd.t);
                if (slot is null) run.SkillStocks.Add(new SkillStock { T = upd.t, N = upd.n });
                else slot.N = upd.n;
            }

        run.Nr = 1;
        // Snapshot the resolved choice-event chain before it's cleared - e==3 realization below
        // replays it to re-derive which confirmed gifts it granted.
        var pceChain = new List<ChoiceEvent>(run.ChoiceEvents);
        run.ChoiceEvents.Clear();
        run.Etype = -1;
        // Every exit rebuilds RewardEvents from the node it clears; the battle-type branches
        // below reassign it, and non-battle nodes (normal battle e==1, shop e==10, gift-less
        // events) leave it empty. Reset here so stale reward events don't leak through.
        run.RewardEvents = new();

        // The shop is fully cleared on every exit (verified 54/54: shop.slots == [] on every
        // captured ExitMapNode record), not just its sold-out entries.
        run.Shop.Slots = new();

        var matchingNode = run.Floor.Nodes.First(n => n.Nid == node.nid);

        run.Eid = matchingNode.Eid;
        var floor = SharedRules.CurrentFloor(run);
        // cnf == matchingNode.F == the 0-based floor index being CLEARED. Distinct from `floor`
        // (LevelAdders.Count), which runs ahead. ALL static-data reward lookups (enemy-buff
        // pool, battle-reward-case tier, constraints, boss-stage rewardList) key on cnf, never
        // on `floor`.
        var cnf = matchingNode.F;

        // Ego-gift level-cap offset - recompute from currently-owned egs (see SharedRules.
        // RecomputeEgmlos / Effects.HiddenGiftLevelBump for the derivation). dul[*].mlos below is
        // sbmlos + egmlos, so this also drives the per-unit cap on floors where egmlos>0.
        SharedRules.RecomputeEgmlos(run);

        // Record the cleared node; event nodes (e==3) also record their event id.
        run.Pnids.Add(matchingNode.Nid);
        if (matchingNode.E == 3) run.Peids.Add(matchingNode.Eid);

        // Event nodes (e==3) realize the deferred confirmed-gift chain here. The exit request
        // carries no gift data - re-derive by replaying the resolved pce chain snapshotted above
        // against a throwaway collector: eid starts at the node's base event id (matchingNode.Eid,
        // == the id just appended to Peids), walking each entry's Nei. Hidden 993xxx gifts already
        // landed in Gifts during UpdateMapNode, so the not-already-present check below naturally
        // skips them; normal 9xxx gifts get added here, one GetConfirmedEgogiftOnWinBattle
        // RewardEvent each, in derivation order. NOTE: grants are a RAW add (not
        // SharedRules.GrantEgoGift's already-owned -> Vestige rule) - the handler's byte-exact
        // behavior for this branch never routes through GrantEgoGift, so this preserves that
        // exactly rather than reinterpreting it.
        if (matchingNode.E == 3)
        {
            var collector = new GiftCollectorSave();
            var chainEid = matchingNode.Eid;
            foreach (var step in pceChain)
            {
                OpenLethe.Server.MdEventManager.ProcessEvent(
                    chainEid,
                    OpenLethe.Server.MdEventManager.ClampChoiceIndex(step.Sl.Count > 0 ? step.Sl[0] : 0),
                    step.Cs,
                    collector);
                chainEid = step.Nei ?? -1;
            }

            var confirmedRre = new List<RewardEvent>();
            foreach (var giftId in collector.GiftIds)
            {
                if (run.Gifts.Items.Any(g => g.Id == giftId)) continue;
                run.Gifts.Items.Add(new EgoGift { Id = giftId });
                confirmedRre.Add(new RewardEvent
                {
                    Rt = "GetConfirmedEgogiftOnWinBattle",
                    Se = 1,
                    Sh = 1,
                    Pool = new() { giftId },
                    // PoolV2/PoolV3 deliberately left null - see RewardEvent.
                });
            }
            if (confirmedRre.Count > 0) run.RewardEvents = confirmedRre;
        }

        // If the node "e" value is 6, move on to the next floor.
        if (matchingNode.E == 6)
        {
            // Floor-scoped hidden 991xxx gifts (granted by this floor's action-choice events)
            // expire when the floor is cleared. 991xxx carry no egmlos bump (only 993xxx do), so
            // this doesn't touch the level cap.
            run.Gifts.Items.RemoveAll(g => g.Id >= 991000 && g.Id < 992000);

            // Leading confirmed gift: the boss stage's own static rewardList (matchingNode.Eid
            // names the stage directly). Collect its EGO_GIFT rewards; if the stage lists any,
            // emit ONE GetConfirmedEgogiftOnWinBattle whose pool is the not-already-owned
            // survivors (may be empty when all are owned - Sh stays 1), and GRANT the survivors
            // into Gifts (this is how 993005 enters Gifts -> drives egmlos). If the stage lists no
            // EGO_GIFT rewards at all, emit nothing. Same RAW-add note as the e==3 branch above:
            // this is a plain AddRange, not SharedRules.GrantEgoGift.
            var bossRewards = MdAbRewards.GetByNodeId(matchingNode.Eid);
            var bossGifts = bossRewards?.Where(r => r.rewardType == "EGO_GIFT").Select(r => r.rewardId).ToList()
                ?? new List<long>();
            RewardEvent? leadingConfirmed = null;
            if (bossGifts.Count > 0)
            {
                var survivors = bossGifts.Where(id => !run.Gifts.Items.Any(g => g.Id == id)).ToList();
                run.Gifts.Items.AddRange(survivors.Select(id => new EgoGift { Id = id }));
                leadingConfirmed = new RewardEvent
                {
                    Rt = "GetConfirmedEgogiftOnWinBattle",
                    Se = 1,
                    Sh = 1,
                    Pool = survivors,
                    PoolV2 = new(),
                    PoolV3 = new(),
                };
            }

            // Recompute egmlos AFTER the boss-gift grant (the earlier recompute ran before it).
            SharedRules.RecomputeEgmlos(run);

            var egoRewards = run.ThemeFloors.Count > 0 ? run.ThemeFloors[^1].Egs : new List<long>();
            var n = egoRewards.Count;
            var levelupValues = Enumerable.Range(0, n).Select(_ => (long)Random.Shared.Next(1, 3)).ToList();

            // The theme-floor pool is consumed on clear; the next floor re-rolls it.
            run.ThemePools = new();

            // Raise the per-run personality level cap. Sbmlos = base + min(clears, cap)*perClear
            // + snft*perNewTheme, all read from the enabled start buffs' effects. Clearing floor
            // cnf means cnf+1 floors cleared. Absolute (idempotent).
            run.LevelOffsets.Sbmlos = LevelOffsets.SbmlosAt(cnf + 1, run.LevelOffsets.Snft);
            var levelCap = run.LevelOffsets.Sbmlos + run.LevelOffsets.Egmlos;
            foreach (var u in run.Party) u.Mlos = levelCap;

            // Record the cleared floor. Difficulty steps every 5 floors: floor/5 + 1, capped at 3.
            var clearedFloor = run.ConstraintScores.Count - 1;
            run.ConstraintScores.Add(new ConstraintScore { Floor = clearedFloor, Difficulty = Math.Min(clearedFloor / 5 + 1, 3) });

            // RewardEvents order (verified): [ leadingConfirmed?, GetBattleRewardCase,
            // GetEgogiftWithEnemyBuf, GetConstraints? ].
            var rre = new List<RewardEvent>();
            if (leadingConfirmed is not null) rre.Add(leadingConfirmed);
            rre.Add(BuildBattleRewardCase(cnf, EncounterCardBonus(run)));
            rre.Add(new RewardEvent
            {
                Rt = "GetEgogiftWithEnemyBuf",
                Se = 2,
                Sh = n,
                Pool = egoRewards,
                // Random n-of-N subset+order of the floor's static enemy-buff pool (masked); the
                // source set is keyed on cnf, byte-guarded by a unit test.
                PoolV2 = OpenLethe.Server.MdMapGen.ChooseMultiple(OpenLethe.Server.MdEnemyBuffPool.ForFloor(cnf), n),
                PoolV3 = levelupValues,
            });

            // Floor-boundary constraints: every entry whose flooridx == cn.F + 1, file order,
            // unfiltered. Only floors 10-14 define any (Se == Sh == pool length).
            var constraints = OpenLethe.Server.MdConstraints.ForFloor(cnf + 1);
            if (constraints.Count > 0)
                rre.Add(new RewardEvent { Rt = "GetConstraints", Se = constraints.Count, Sh = constraints.Count, Pool = constraints, PoolV2 = new(), PoolV3 = new() });

            run.RewardEvents = rre;
        }

        // Hard abno battle.
        if (matchingNode.E == 14)
        {
            var rewards = MdAbRewards.GetByNodeId(matchingNode.Eid);
            if (rewards is { Count: > 0 })
            {
                var rewardEgos = rewards.Where(r => r.rewardType == "EGO_GIFT").Select(r => r.rewardId).ToList();
                run.Gifts.Items.AddRange(rewardEgos.Select(id => new EgoGift { Id = id }));
                run.RewardEvents = rewardEgos.Select(id => new RewardEvent
                {
                    Rt = "GetConfirmedEgogiftOnWinBattle",
                    Se = 1,
                    Sh = 1,
                    Pool = new() { id },
                    PoolV2 = new(),
                    PoolV3 = new(),
                }).ToList();
            }
        }

        // Abno battle or hard battle: RewardEvents order (verified) [ GetEgogift, GetBattleRewardCase ].
        if (matchingNode.E == 5 || matchingNode.E == 2)
        {
            run.RewardEvents = new()
            {
                new RewardEvent
                {
                    Rt = "GetEgogift",
                    Se = 1,
                    Sh = 1,
                    Pool = new MdThemePool().SelectRandomEgosFromPool(SharedRules.ThemePackId(run), 1, floor),
                    PoolV2 = new(),
                    PoolV3 = new(),
                },
                BuildBattleRewardCase(cnf, EncounterCardBonus(run)),
            };
        }

        run.Cost += OpenLethe.Server.MdCost.GetDefaultCost(matchingNode.E, floor);

        // Echo the client's abno logs sorted by id. The server regenerates the RNG content
        // (k, s, p, ps[*].atrr/atkr) which the replay masks; the deterministic outer length +
        // per-entry id + ps ids/count come straight from the request.
        var sortedAbnoLogs = abnormalityLogs
            .Where(a => a is not null)
            .OrderBy(a => a!["id"]!.GetValue<long>())
            .Cast<object>()
            .ToList();

        return new NodeExitResult(sortedAbnoLogs);
    }
}
