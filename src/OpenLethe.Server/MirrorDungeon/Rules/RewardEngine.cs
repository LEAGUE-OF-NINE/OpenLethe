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
        List<JsonNode> abnormalityLogs,
        IReadOnlyList<ThemeStatic>? customThemes = null)
    {
        run.Floor.Current = new CurrentPosition { F = node.f, S = node.s, Nid = node.nid };

        // Who could fight this encounter: the sinners standing before the client's battle result
        // lands (Milepost of Survival counts the participants, and the request lists everyone).
        var livingBeforeBattle = run.Party.Count(u => u.CurrentHp > 0);

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
        // Deduped: while an event battle finishes, the client re-exits the very same node, and the
        // retail save never repeats an id here (run-2 keeps pnids unique). The repeated entries made
        // the client believe it had not advanced.
        if (!run.Pnids.Contains(matchingNode.Nid)) run.Pnids.Add(matchingNode.Nid);
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
                if (SharedRules.OwnsGift(run, giftId)) continue;
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
                var survivors = bossGifts.Where(id => !SharedRules.OwnsGift(run, id)).ToList();
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

            // The theme-floor pool is consumed on clear; the next floor re-rolls it. Hard runs roll
            // it in the boss-reward step (retail: tfps empty here, verified). A normal run rolls it
            // right away, so its next selection never hinges on that hard-mode enemy-buff popup.
            run.ThemePools = MapGenerator.IsNormalMode(run) ? MapGenerator.NextFloorPools(run, customThemes) : new();

            // Raise the per-run personality level cap. Sbmlos = base + min(clears, cap)*perClear
            // + snft*perNewTheme, all read from the enabled start buffs' effects. Clearing floor
            // cnf means cnf+1 floors cleared. Absolute (idempotent).
            run.LevelOffsets.Sbmlos = StartBuffEffects.LevelOffsets(run).SbmlosAt(cnf + 1, run.LevelOffsets.Snft);
            var levelCap = run.LevelOffsets.Sbmlos + run.LevelOffsets.Egmlos;
            foreach (var u in run.Party) u.Mlos = levelCap;

            // Record the cleared floor. The number comes from the run's own position (cnf), NOT from
            // the list length: a floor that was jumped over never clears, so counting entries drifts
            // and leaves the client with a cfs whose floors do not match where the run actually is.
            // A floor the player spent Adversity picks on keeps THAT score: AcquireConstraints wrote
            // it onto this floor's entry, and the old code overwrote it with the act tier here, which
            // is why the running total collapsed back to 1/2/3 after every clear. Floors without a
            // pick fall back to the act tier (floor/5 + 1, capped at 3 - matches the retail capture:
            // the act-1 boss at floor 4 records difficulty 1, the act-2 boss at floor 9 records 2).
            var clearedFloor = cnf;
            var pickedHere = run.ConstraintSelections
                .Where(s => s.Flooridx == clearedFloor)
                .SelectMany(s => s.Ids)
                .ToList();
            var clearedDifficulty = pickedHere.Count > 0
                ? MdConstraints.TotalScore(pickedHere)
                : Math.Min(clearedFloor / 5 + 1, 3);
            var clearedEntry = run.ConstraintScores.FirstOrDefault(c => c.Floor == clearedFloor);
            if (clearedEntry is null)
                run.ConstraintScores.Add(new ConstraintScore { Floor = clearedFloor, Difficulty = clearedDifficulty });
            else
                clearedEntry.Difficulty = clearedDifficulty;

            // RewardEvents order (verified): [ leadingConfirmed?, GetBattleRewardCase,
            // GetEgogiftWithEnemyBuf, GetConstraints? ].
            var rre = new List<RewardEvent>();
            if (leadingConfirmed is not null) rre.Add(leadingConfirmed);
            rre.Add(BuildBattleRewardCase(cnf, EncounterCardBonus(run)));
            rre.Add(new RewardEvent
            {
                Rt = "GetEgogiftWithEnemyBuf",
                // Picks allowed: a hard pack's clear bonus offers 2 (capture-verified), a normal
                // pack's offers only 1.
                Se = MapGenerator.IsNormalMode(run) ? 1 : 2,
                Sh = n,
                Pool = egoRewards,
                // Random n-of-N subset+order of the floor's static enemy-buff pool (masked); the
                // source set is keyed on cnf, byte-guarded by a unit test.
                PoolV2 = OpenLethe.Server.MdMapGen.ChooseMultiple(OpenLethe.Server.MdEnemyBuffPool.ForFloor(cnf), n),
                PoolV3 = levelupValues,
            });

            // Floor-boundary constraints: every entry whose flooridx == cn.F + 1, file order,
            // unfiltered by the pool itself - but the table's replacementRules are applied against
            // what the run already picked, exactly like the client does before rendering the list.
            // Skipping that step made the server offer base ids while the client showed the swapped
            // ones, so a pick landed on a different constraint (pick one, get another).
            var ownedConstraints = run.ConstraintSelections.SelectMany(s => s.Ids);
            var constraints = OpenLethe.Server.MdConstraints.ForFloor(cnf + 1, ownedConstraints);
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
                // Through GrantEgoGift, not a raw add: an owned gift (or fusion component) turns
                // into its tier's Vestige instead of landing a second copy.
                foreach (var id in rewardEgos) SharedRules.GrantEgoGift(run, id);
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

        // Hidden battle (e==15, an event revealed on entry by MapNodeRules.TryRevealHiddenBattle). From
        // the Mirror Dungeon fork: the stage's own battle file names its exclusive E.G.O gift, which
        // goes straight into egs (no confirm popup - the client refuses one for a gift the account
        // already got); only the battle reward case is queued. Its cost is the common table's
        // HIDDEN_BATTLE entry, paid with every other battle's below.
        if (matchingNode.E == OpenLethe.Server.MdMapGen.HiddenBattleNodeType
            && OpenLethe.Server.MdMapGen.IsHiddenBattle(matchingNode.Eid))
        {
            var hiddenGiftIds = OpenLethe.Server.MdMapGen.HiddenGiftsOf(matchingNode.Eid)
                .Where(id => !SharedRules.OwnsGift(run, id))
                .ToList();
            run.Gifts.Items.AddRange(hiddenGiftIds.Select(id => new EgoGift { Id = id }));

            // A hidden-boss clear revives the party (md-extreme-run-2 seq339: every dead unit
            // comes back at 10000 with cm 0).
            SharedRules.ReviveAll(run);

            run.RewardEvents = new() { BuildBattleRewardCase(cnf, EncounterCardBonus(run)) };
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
                    Pool = new MdThemePool(customThemes).SelectRandomEgosFromPool(SharedRules.ThemePackId(run), 1, floor),
                    PoolV2 = new(),
                    PoolV3 = new(),
                },
                BuildBattleRewardCase(cnf, EncounterCardBonus(run)),
            };
        }

        // Pay what the client shows on the node: the common table's victory cost for this
        // encounter type and difficulty, weighted by the floor being cleared (cnf - `floor` runs
        // ahead of it, which paid the wrong row and nothing at all for bosses past floor 4), plus
        // the 9079 Golden Urn / 9242 Bongy Plush bonuses and Binary Star-shop's Encounter Reward
        // Cost +20/30/40%.
        var costGain = MdCost.BattleVictoryCost(matchingNode.E, cnf, hard: !MapGenerator.IsNormalMode(run));
        run.Cost += MdCost.ApplyCostGifts(run, matchingNode.E, costGain);
        run.Cost += MdCost.MilepostOfSurvivalBonus(run, matchingNode.E, matchingNode.Eid, livingBeforeBattle);

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
