using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Model;

namespace OpenLethe.Server.MirrorDungeon.Rules;

// The reward-RESOLUTION group: popups queued by ExitMapNode's rre engine get resolved here.
// Unlike ExitMapNode's raw-add (RewardEngine), these endpoints grant via SharedRules.GrantEgoGift
// - the already-owned -> Vestige (9991-9995, the gift's tier) rule. Verbatim ports of
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
    // A popup with se > 1 (the start buffs' Perfected Possibility / floor gifts) takes up to se
    // distinct indices from one request; a battle's se == 1 popup keeps taking just the first.
    public static List<EgoGift> AcquireReward(Run run, List<long> selectIndexList)
    {
        var ev = run.RewardEvents.FirstOrDefault(e => e.Rt == "GetEgogift")
            ?? throw new KeyNotFoundException("no GetEgogift reward event queued");
        var indices = (selectIndexList.Count > 0 ? selectIndexList : new List<long> { 0 })
            .Distinct()
            .Take((int)Math.Max(1, ev.Se))
            .ToList();
        // Range-check in long before any int cast (a huge index must not wrap into range).
        if (indices.Any(i => i < 0 || i >= ev.Pool.Count))
            throw new KeyNotFoundException("selectIndexList out of range for GetEgogift pool");

        var granted = new List<EgoGift>();
        foreach (var index in indices)
        {
            SharedRules.GrantEgoGift(run, ev.Pool[(int)index]);
            granted.Add(run.Gifts.Items[^1]);
        }
        // Consume ONLY the GetEgogift popup; any sibling reward events queued alongside it
        // (GetBattleRewardCase, GetConfirmedEgogiftOnWinBattle) stay in RewardEvents and are
        // echoed back as remainRewardEvent by the caller (capture seq54/84/107/192).
        run.RewardEvents.Remove(ev);
        return granted;
    }

    // Verbatim port of RejectRewardEgoGiftsMirrorDungeon (Handlers/MirrorDungeonShop.cs
    // :237-256): clears the WHOLE reward-event queue (not just one popup) and grants nothing.
    public static void RejectReward(Run run) => run.RewardEvents.Clear();

    // Verbatim port of AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon (Handlers/
    // MirrorDungeonRewards.cs :156-211). Grant order (verified seq43/241): ALL pool picks first,
    // THEN all pool_v2 picks - grouped, not interleaved. Returns the actually-granted entries
    // (post Vestige-transform) plus the run's full LevelAdders (same list instance run.LevelAdders
    // - the caller doesn't need a copy, just the count for its DTO).
    // customThemes: the account's uploaded packs, for the bonus pick and the next floor's pool.
    public static (List<EgoGift> Granted, List<long> LevelAdders) AcquireWithEnemyBuf(Run run, List<long> selectIndexList,
        IReadOnlyList<OpenLethe.Server.ThemeStatic>? customThemes = null)
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

            // Picks whose enemy levels (pool_v3) add up to minEnemyLevelBuffForAdditionalEgoGift (4:
            // two +2 picks) earn one more gift: a 1-of-3 GetEgogift from the cleared floor's pack,
            // queued after every remaining event. Retail does exactly this on every capture whose
            // picks sum to 4 (md-extreme-run seq106/191, run-2 seq44/128/170/208/373/402/430) and
            // never on a 3 or less.
            var poolV3 = popup.PoolV3 ?? new List<long>();
            var enemyLevels = selectIndexList.Distinct()
                .Sum(index => index >= 0 && index < poolV3.Count ? poolV3[(int)index] : 0);
            if (enemyLevels >= MdCommonTable.MinEnemyLevelForAdditionalGift)
            {
                var bonus = new MdThemePool(customThemes)
                    .SelectRandomEgosFromPool(SharedRules.ThemePackId(run), 3, SharedRules.CurrentFloor(run));
                if (bonus.Count > 0)
                    run.RewardEvents.Add(new RewardEvent
                    {
                        Rt = "GetEgogift", Se = 1, Sh = bonus.Count, Pool = bonus, PoolV2 = new(), PoolV3 = new(),
                    });
            }
        }

        // The boss clear rolls the theme-floor pool for the NEXT floor (cn.f + 1): 4 themes, idx
        // == the next floor's act - identical CONTENT rule to RecreateThemeFloorPool, but NOT via
        // MapGenerator.RecreateThemePool: that helper ALSO bumps TfpsCreated and clears StartPools
        // (RecreateThemeFloorPoolMirrorDungeon's own documented behavior). This endpoint does
        // neither - verified byte-exact across all 10 captured records (fixtures seq43/106/191/
        // ...: tfpsCreated stays whatever it already was, seps is never touched here). Using the
        // shared helper wholesale would regress the byte-green replay, so this inlines just the
        // tfps assignment.
        run.ThemePools = MapGenerator.NextFloorPools(run, customThemes);

        return (granted, run.LevelAdders);
    }

    // /api/RejectRewardEgoGiftWithEnemyBufsMirrorDungeon: the boss reward skipped. It used to be a
    // stateless stub, so the popup stayed queued and - since the next floor's packs are rolled by
    // the acquire above - the theme selection that followed a skip offered nothing. A skip is the
    // same floor clear minus the gifts: the boss-clear level adders, the popup consumed, the next
    // pool rolled.
    public static List<long> RejectWithEnemyBuf(Run run, IReadOnlyList<OpenLethe.Server.ThemeStatic>? customThemes = null)
    {
        var popup = run.RewardEvents.FirstOrDefault(re => re.Rt == "GetEgogiftWithEnemyBuf");
        if (popup is not null)
        {
            run.LevelAdders.Add(1);
            run.LevelAdders.Add(1);
            run.RewardEvents.Remove(popup);
        }
        run.ThemePools = MapGenerator.NextFloorPools(run, customThemes);
        return run.LevelAdders;
    }

    // Verbatim port of AcquireMirrorDungeonBattleReward (Handlers/MirrorDungeonRewards.cs
    // :213-288). Consumes ONLY the GetBattleRewardCase popup(s); every sibling reward event (a
    // leading GetConfirmedEgogiftOnWinBattle, the GetEgogiftWithEnemyBuf, a trailing
    // GetConstraints) stays queued for the later reward endpoints. tfps is left untouched (it was
    // cleared on the floor clear and is re-rolled later by AcquireRewardEgoGiftsWithEnemyBuf -
    // verified 0 on all 12 records).
    // customThemes: the account's uploaded packs, so a card on a custom pack's floor draws from it.
    public static void AcquireBattleReward(Run run, List<long> selectIndexList,
        IReadOnlyList<OpenLethe.Server.ThemeStatic>? customThemes = null)
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
                    if (RewardRandomEgoGift(run, customThemes, rp.egoGiftAcquirableProb ?? 0, rp.egoGiftTierRange) is { } startEgo)
                        SharedRules.GrantEgoGift(run, startEgo);
                    break;

                case "COST":
                    run.Cost += RollCost(rp);
                    break;

                case "EGOGIFT":
                    if (RewardRandomEgoGift(run, customThemes, rp.egoGiftAcquirableProb ?? 0, rp.egoGiftTierRange) is { } ego)
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

                case "STARLIGHT_MIN_MAX":
                    // group-D cards 401-407, whose EN text is "Gain additional {0} Starlight upon
                    // clearing the Mirror Dungeon" (limbus_locale EN uiList.json ->
                    // dungeon_battle_reward_case_format_chip_acquisition). The roll is banked in the
                    // run's starlight tally (scc) - the same field EnableStartBuff adds leftover
                    // start-buff points to - and settles into the account's owned Starlight (pslp)
                    // when the run ends (AcquireExitReward). Upstream's Reward enum had only 4
                    // variants, so a picked group-D card fell off the end of this switch and
                    // silently did nothing.
                    // With owned starlight pinned (StarlightState.PinnedPslp) the banked gain would be
                    // invisible, so the pin is raised by the roll as well (from the Mirror Dungeon fork).
                    if (rp.bonusStarlight is { } sl && sl.max >= sl.min)
                    {
                        var rolled = Random.Shared.NextInt64(sl.min, sl.max + 1);
                        run.Starlight.Scc += rolled;
                        if (StarlightState.PinnedPslp is long pin && run.Starlight.Pslp < pin + rolled)
                            run.Starlight.Pslp = pin + rolled;
                    }
                    // Star-shower+/++: "When Choosing Starlight from Encounter Reward Cards, Cost +75/+150".
                    run.Cost += StartBuffEffects.StarlightCardCost(run);
                    break;
            }
        }

        run.RewardEvents.RemoveAll(re => re.Rt == "GetBattleRewardCase");
    }

    // Port of PreviewMirrorDungeonExitReward (Handlers/MirrorDungeonShop.cs :260-278).
    // The 4-option table math itself lives in MdExitReward.BuildOptions (MdTheme.cs) and stays
    // there per the migration brief - this just calls through. totalConstraintScore used to be a
    // hardcoded 0 (the captured record has no constraints selected, so 0 was right for it); it is
    // now the sum of the picked constraints' score column, which is what the client multiplies the
    // run's Starlight/MDPass payout by (mirrordungeon-constraint-07.json rewardMultiplier = 15).
    public static (List<Wire.ExitRewardOption> Options, long TotalConstraintScore) PreviewExitReward(Run run) =>
        (OpenLethe.Server.MdExitReward.BuildOptions(), MdConstraints.TotalScore(run.ConstraintSelections.SelectMany(s => s.Ids)));

    // Verbatim port of AcquireMirrorDungeonExitReward (Handlers/MirrorDungeonShop.cs :280-323).
    // Run end: every party unit's isp flips to 1, uniformly for participants and
    // non-participants alike (capture seq314 all isp=0 -> seq321 all isp=1) - a flat run-end
    // flag like isEndDungeon, NOT part of the dul[*].pord permutation (that stays BLOCKED-masked,
    // untouched here - see ReplayMasks). useEnkephalinModule is unread, matching the
    // pre-migration handler (which never inspected p.useEnkephalinModule either).
    public static List<Wire.Element> AcquireExitReward(Run run, bool useEnkephalinModule, long chanceConsumption)
    {
        foreach (var unit in run.Party) unit.Isp = 1;

        // Settle the run's banked starlight (scc: the group-D "additional Starlight upon clearing"
        // cards plus leftover start-buff points) into the account's owned Starlight (pslp). The
        // handler snapshots the tally before this call for starlightChangeAmount; the run save is
        // wiped right after this response, so this is the last place it can be consumed.
        run.Starlight.Pslp += run.Starlight.Scc;
        run.Starlight.Scc = 0;

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

    /// Port of acquire_mirror_dungeon_battle_reward.rs reward_random_ego_gift. Rust looked for the
    /// drop pool with dungeonId == 5, which no shipped file has, so these cards never granted a
    /// gift; drawing from the whole season pool instead handed out other packs' exclusive gifts.
    /// The gift comes from the current floor's pack (MdThemePool.ThemeDropPool - its egoGiftPool
    /// and exclusives, custom packs included), within the card's tier range.
    private static long? RewardRandomEgoGift(Run run, IReadOnlyList<OpenLethe.Server.ThemeStatic>? customThemes,
        double acquirableProb, MdTierRange? tierRange)
    {
        if (tierRange is null) return null;
        if (Random.Shared.NextDouble() > acquirableProb) return null;

        var egos = new MdThemePool(customThemes).ThemeDropPool(SharedRules.ThemePackId(run))
            .Where(id => tierRange.WithinRange(OpenLethe.Server.MdEgoData.DetermineEgoTier(id) ?? 0))
            .ToList();
        return egos.Count == 0 ? null : egos[Random.Shared.Next(egos.Count)];
    }
}
