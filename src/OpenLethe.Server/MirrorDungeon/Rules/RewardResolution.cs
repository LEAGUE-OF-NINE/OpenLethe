using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Model;

namespace OpenLethe.Server.MirrorDungeon.Rules;

// The reward-RESOLUTION group: popups queued by ExitMapNode's rre engine get resolved here.
// Unlike ExitMapNode's raw-add (RewardEngine), these endpoints grant via SharedRules.GrantEgoGift
// - the already-owned -> Vestige (9992 TIER_2 / 9993 TIER_3) rule. Verbatim ports of
// Handlers/MirrorDungeonShop.cs AcquireRewardEgoGiftsMirrorDungeon (:204-235) /
// RejectRewardEgoGiftsMirrorDungeon (:237-256) and Handlers/MirrorDungeonRewards.cs
// AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon (:156-211) / AcquireMirrorDungeonBattleReward
// (:213-288). See the handler-migration Task 9 brief.
public static class RewardResolution
{
    // Verbatim port of AcquireRewardEgoGiftsMirrorDungeon (Handlers/MirrorDungeonShop.cs
    // :204-235). The handler's two 500-branches (no queued GetEgogift event; selectIndexList[0]
    // out of the pool's range) become a KeyNotFoundException the caller catches - same pattern
    // already established by MapGenerator.GenerateFloor (a genuine lookup miss, not a
    // deliberately-invented control-flow exception). Returns the actually-granted entry (post
    // Vestige-transform, not necessarily the raw pool id) - the DTO itself still reads the full
    // egs list off ToWire(run), same as before.
    public static List<EgoGift> AcquireReward(Run run, List<long> selectIndexList)
    {
        var ev = run.RewardEvents.FirstOrDefault(e => e.Rt == "GetEgogift")
            ?? throw new KeyNotFoundException("no GetEgogift reward event queued");
        var index = (int)(selectIndexList.Count > 0 ? selectIndexList[0] : 0);
        if (index < 0 || index >= ev.Pool.Count)
            throw new KeyNotFoundException("selectIndexList out of range for GetEgogift pool");

        SharedRules.GrantEgoGift(run, ev.Pool[index]);
        // Consume ONLY the GetEgogift popup; any sibling reward events queued alongside it
        // (GetBattleRewardCase, GetConfirmedEgogiftOnWinBattle) stay in RewardEvents and are
        // echoed back as remainRewardEvent by the caller (capture seq54/84/107/192).
        run.RewardEvents.Remove(ev);
        return new List<EgoGift> { run.Gifts.Items[^1] };
    }

    // Verbatim port of RejectRewardEgoGiftsMirrorDungeon (Handlers/MirrorDungeonShop.cs
    // :237-256): clears the WHOLE reward-event queue (not just one popup) and grants nothing.
    public static void RejectReward(Run run) => run.RewardEvents.Clear();

    // Verbatim port of AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon (Handlers/
    // MirrorDungeonRewards.cs :156-211). Grant order (verified seq43/241): ALL pool picks first,
    // THEN all pool_v2 picks - grouped, not interleaved. Returns the actually-granted entries
    // (post Vestige-transform) plus the run's full LevelAdders (same list instance run.LevelAdders
    // - the caller doesn't need a copy, just the count for its DTO).
    public static (List<EgoGift> Granted, List<long> LevelAdders) AcquireWithEnemyBuf(Run run, List<long> selectIndexList)
    {
        // Per boss-clear, two per-unit level-up amounts are appended to leveladders (count 2
        // verified on all 10 records). The VALUES are per-unit battle-driven level gains we can't
        // reproduce - masked in the replay, same class as statistics. The COUNT is what matters.
        // ponytail: value 1 is a masked placeholder; the real amount needs the level-up engine.
        run.LevelAdders.Add(1);
        run.LevelAdders.Add(1);

        var granted = new List<EgoGift>();
        var popup = run.RewardEvents.FirstOrDefault(re => re.Rt == "GetEgogiftWithEnemyBuf");
        if (popup is not null)
        {
            // Range-check in long before any int cast so a huge client index stays out-of-range
            // (a miss grants id-0, via GrantEgoGift, same as the pre-migration wire twin).
            var poolV2 = popup.PoolV2 ?? new List<long>();
            foreach (var index in selectIndexList)
            {
                SharedRules.GrantEgoGift(run, index >= 0 && index < popup.Pool.Count ? popup.Pool[(int)index] : 0);
                granted.Add(run.Gifts.Items[^1]);
            }
            foreach (var index in selectIndexList)
            {
                SharedRules.GrantEgoGift(run, index >= 0 && index < poolV2.Count ? poolV2[(int)index] : 0);
                granted.Add(run.Gifts.Items[^1]);
            }
            // Consume only this popup; any sibling reward events (a leading
            // GetConfirmedEgogiftOnWinBattle, a trailing GetConstraints) stay queued.
            run.RewardEvents.Remove(popup);
        }

        // The boss clear rolls the theme-floor pool for the NEXT floor (cn.f + 1): 4 themes, idx
        // == the next floor's act - identical CONTENT rule to RecreateThemeFloorPool, but NOT via
        // MapGenerator.RecreateThemePool: that helper ALSO bumps TfpsCreated and clears StartPools
        // (RecreateThemeFloorPoolMirrorDungeon's own documented behavior). This endpoint does
        // neither - verified byte-exact across all 10 captured records (fixtures seq43/106/191/
        // ...: tfpsCreated stays whatever it already was, seps is never touched here). Using the
        // shared helper wholesale would regress the byte-green replay, so this inlines just the
        // tfps assignment.
        run.ThemePools = OpenLethe.Server.MdMapGen.RecreateThemes(run.Floor.Current.F + 1)
            .Select(t => new ThemePool { Idx = t.idx, Tfid = t.tfid, Egs = t.egs, Upegs = t.upegs })
            .ToList();

        return (granted, run.LevelAdders);
    }

    // Verbatim port of AcquireMirrorDungeonBattleReward (Handlers/MirrorDungeonRewards.cs
    // :213-288). Consumes ONLY the GetBattleRewardCase popup(s); every sibling reward event (a
    // leading GetConfirmedEgogiftOnWinBattle, the GetEgogiftWithEnemyBuf, a trailing
    // GetConstraints) stays queued for the later reward endpoints. tfps is left untouched (it was
    // cleared on the floor clear and is re-rolled later by AcquireRewardEgoGiftsWithEnemyBuf -
    // verified 0 on all 12 records).
    public static void AcquireBattleReward(Run run, List<long> selectIndexList)
    {
        var cards = run.RewardEvents
            .Where(re => re.Rt == "GetBattleRewardCase")
            .SelectMany(re => re.Pool)
            .ToList();

        foreach (var index in selectIndexList)
        {
            // Range-check in long before any int cast - a huge client-supplied index (e.g.
            // 4294967296) must stay out-of-range, not wrap to a valid one.
            if (index < 0 || index >= cards.Count) continue; // Rust filter_map drops misses
            if (!MdEncounterCard.EncounterRewardMap.TryGetValue(cards[(int)index], out var reward)) continue;
            var rp = reward.rewardParams;
            if (rp is null) continue;

            switch (reward.rewardType)
            {
                case "COST_EGOGIFT_START_CATEGORY":
                    // Adds cost AND, with egoGiftAcquirableProb, a random-tier gift straight into
                    // Gifts - NO reward event is queued here (capture-verified: no
                    // GetConfirmedEgogift is queued, unlike the confirmed-gift event path).
                    run.Cost += RollCost(rp);
                    if (RewardRandomEgoGift(rp.egoGiftAcquirableProb ?? 0, rp.egoGiftTierRange) is { } startEgo)
                        SharedRules.GrantEgoGift(run, startEgo);
                    break;

                case "COST":
                    run.Cost += RollCost(rp);
                    break;

                case "EGOGIFT":
                    if (RewardRandomEgoGift(rp.egoGiftAcquirableProb ?? 0, rp.egoGiftTierRange) is { } ego)
                        SharedRules.GrantEgoGift(run, ego);
                    break;

                case "EGOSTOCK":
                    if (rp.leastEgoStock is not { } least) break; // randomEgoStock is ignored upstream
                    var stock = new Dictionary<string, long>
                    {
                        ["CR"] = 0, ["SC"] = 0, ["AM"] = 0, ["SH"] = 0,
                        ["AZ"] = 0, ["IN"] = 0, ["VI"] = 0,
                    };
                    foreach (var ess in run.SkillStocks) stock[ess.T] = ess.N;

                    // Rust sorts a Vec built from a HashMap (random iteration order), so ties
                    // break randomly there; ThenBy(Random) reproduces that here.
                    foreach (var key in stock.OrderBy(kv => kv.Value).ThenBy(_ => Random.Shared.Next()).Take((int)least.kind).Select(kv => kv.Key).ToList())
                        stock[key] += least.num;

                    run.SkillStocks = stock.Select(kv => new SkillStock { T = kv.Key, N = kv.Value }).ToList();
                    break;
            }
        }

        run.RewardEvents.RemoveAll(re => re.Rt == "GetBattleRewardCase");
    }

    // Verbatim port of PreviewMirrorDungeonExitReward (Handlers/MirrorDungeonShop.cs :260-278).
    // The 4-option table math itself lives in MdExitReward.BuildOptions (MdTheme.cs) and stays
    // there per the migration brief - this just calls through. totalConstraintScore is hardcoded
    // 0 in the pre-migration handler too: no constraint-score formula is wired anywhere in this
    // codebase yet, and the run's scinfos never selects any ids either, so 0 is correct for the
    // captured record regardless of run state (see MdExitReward.BuildOptions doc comment).
    public static (List<Wire.ExitRewardOption> Options, long TotalConstraintScore) PreviewExitReward(Run run) =>
        (OpenLethe.Server.MdExitReward.BuildOptions(), 0);

    // Verbatim port of AcquireMirrorDungeonExitReward (Handlers/MirrorDungeonShop.cs :280-323).
    // Run end: every party unit's isp flips to 1, uniformly for participants and
    // non-participants alike (capture seq314 all isp=0 -> seq321 all isp=1) - a flat run-end
    // flag like isEndDungeon, NOT part of the dul[*].pord permutation (that stays BLOCKED-masked,
    // untouched here - see ReplayMasks). useEnkephalinModule is unread, matching the
    // pre-migration handler (which never inspected p.useEnkephalinModule either).
    public static List<Wire.Element> AcquireExitReward(Run run, bool useEnkephalinModule, long chanceConsumption)
    {
        foreach (var unit in run.Party) unit.Isp = 1;

        // The client applies EXP/BATTLEPASS_POINT/USERBANNER_RECORD elements via `updated`
        // (masked wholesale in the replay) - only ITEM-type elements are echoed back here
        // (capture-verified: chanceConsumption 3 -> [{ITEM,2,750},{ITEM,20041,150}]).
        var chosen = OpenLethe.Server.MdExitReward.BuildOptions()
            .FirstOrDefault(o => o.chanceConsumption == chanceConsumption) ?? new Wire.ExitRewardOption();
        return chosen.rewardList.Where(e => e.type_ == "ITEM").ToList();
    }

    // Verbatim port of ExitMirrorDungeon (Handlers/MirrorDungeonRewards.cs :128-154). Flat
    // isEndDungeon=1 (isclear=1 is a DTO-only literal the handler sets directly, not persisted
    // save state - see the handler). statistics is echoed unmodified via the ToWire round-trip.
    public static void ExitMirrorDungeon(Run run) => run.IsEndDungeon = 1;

    private static long RollCost(MdRewardParams rp) =>
        Random.Shared.NextInt64(rp.acquireCostMin ?? 0, (rp.acquireCostMax ?? 0) + 1);

    /// Port of acquire_mirror_dungeon_battle_reward.rs reward_random_ego_gift.
    /// ponytail: UPSTREAM QUIRK, PRESERVED - Rust looks for the drop pool with dungeonId == 5,
    /// but the only shipped file is mirrordungeon-egogift-droppool-7.json (dungeonId 7). So this
    /// always returns null on real data and EGOGIFT / COST_EGOGIFT_START_CATEGORY rewards never
    /// grant a gift. Do not "fix" it to the max-dungeon-id lookup MdThemePool uses - that is a
    /// different code path. The full roll below stays ported so the behaviour is right if a
    /// dungeon-5 pool ever ships.
    private static long? RewardRandomEgoGift(double acquirableProb, MdTierRange? tierRange)
    {
        var pool = OpenLethe.Server.MdThemeData.DropPools().FirstOrDefault(p => p.dungeonId == 5);
        if (pool is null || tierRange is null) return null;
        if (Random.Shared.NextDouble() > acquirableProb) return null;

        var egos = pool.egoGifts
            .Where(id => tierRange.WithinRange(OpenLethe.Server.MdEgoData.DetermineEgoTier(id) ?? 0))
            .ToList();
        return egos.Count == 0 ? null : egos[Random.Shared.Next(egos.Count)];
    }
}
