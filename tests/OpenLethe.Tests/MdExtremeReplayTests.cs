using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server.Auth;
using OpenLethe.Tests.Replay;
using Xunit;

[Collection("postgres")]
public class MdExtremeReplayTests(PostgresFixture db)
{
    // Endpoints this slice asserts (already correct). Grows each slice until it's all of
    // them. The 5 seeded here all turned out to have genuine deterministic divergences on
    // the first replay run (not RNG - see task-5-report.md for the full evidence), so none
    // survive triage this slice. Each is de-covered with the offending field(s) below.
    private static readonly HashSet<string> Covered = new()
    {
        // cycle8 task-6: cost is now per-dungeonId (200 for id 7) and the shop is the NEW
        // slots[] model - captured ENTER shop is empty, so it byte-matches with no RNG mask.
        "/api/EnterMirrorDungeon",
        // cycle8 task-10: prevChoiceEvent now echoes the accumulated pce (appended at the tail,
        // sl/cs/ri taken from the request instead of a hardcoded cs=-1/ri=0, not reset to a
        // fresh list per call) and dungeonUnitList reuses the ExitMapNode dul-merge helper
        // (also fixed a genuine engine bug along the way: ResultForm.nextBattleId never bound
        // to the real "nextBattleID" JSON key, so StartBattle_Abnormality always produced
        // nei=-1 - fixture seq20/77/140 prove the real server does resolve it). BUT
        // UpdateMapNode still can't be Covered - it diverges on two things outside this task's
        // scope, both non-RNG:
        //   - cels (EVERY one of the 37 records): a ChoiceEventLogFormat[] {eventId, choiceIdx,
        //     count} baked into MirrorOriginSaveInfo.currentInfo.cels per the real client
        //     contract (packets/_shared.cs). The `count` values (e.g. eid 901021 -> [7,2,2])
        //     are not derivable from any static-data field we have access to (checked
        //     mirror-dungeon-{action,personality}-choice-event.json - no count-shaped field
        //     exists) and don't look like RNG. Left at `new()` - needs investigation.
        //   - currentEgoGifts: GetConfirmedEgogift rewards for a NORMAL 9xxx gift are queued,
        //     not pushed immediately - fixture seq27->28 (gift 9040) and seq48->49 (gift 9012)
        //     both push via MdEventManager during the UpdateMapNode call, but the gift only
        //     appears in the FOLLOWING ExitMapNode's currentInfo.egs (seq29/seq50), not in this
        //     endpoint's own response. Only the "hidden" 993xxx level-cap gift (993003 at
        //     seq227) lands immediately. This is exactly the "ExitMapNode e==3 event-node
        //     reward work" the brief calls out as the next task - the deferred-grant queue
        //     doesn't exist yet, so this task can't wire it correctly. Also inherits the
        //     already-documented egs[*].oid gap from the shop/purchase engine below.
        // See task-10-report.md for the full per-seq evidence.
        // "/api/UpdateMirrorDungeonMapNode",
        // cycle8 task-9: cost + usedcost are now CEILING-masked (unknown battle-performance
        // formula) and abnormalityLogs is narrow-masked (the log list is echoed sorted-by-id so
        // length/id/ps structure is byte-verified; only the RNG k/s/p/atrr/atkr are masked).
        // efs.egmlos is now DERIVED (sum of hidden-gift level bumps over owned egs - holds on
        // every captured record). But ExitMapNode STILL can't be Covered: its full currentInfo
        // diverges on two large subsystems this task does not own -
        //   - rre/egs reward+event engine (rre.<length>, egs.<length>): event nodes (e==3) grant
        //     confirmed ego gifts via the deferred choice-event engine; floor clears (e==6) queue
        //     GetBattleRewardCase + a floor-boss hidden gift (993005, keyed by node->battle-stage
        //     reward we don't map) and seq294 adds GetConstraints; abno (e==14) grants battle
        //     gifts. This also strands dul[*].mlos at seq189 (needs the un-acquired 993005 for
        //     egmlos=3).
        //   - shop/purchase/upgrade engine (shop.slots.<length>, egs[*].oid, egs[*].ul): the
        //     e==10 shop-spend + PurchaseEgoGift/UpgradeEgoGift accounting (oid = server order id,
        //     ul = upgrade level), de-covered in prior cycle-8 tasks.
        // See task-9-report.md for the full remaining-diff list and derived rules.
        // cycle8 task-11b: the reward-event engine is complete. Every ExitMapNode record
        // byte-matches. DETERMINISTIC + byte-verified: shop.slots cleared on exit; rre STRUCTURE
        // (length, per-entry rt/se/sh, pool LENGTH == sh); the leading GetConfirmedEgogiftOnWinBattle
        // on e==6 (boss-stage rewardList EGO_GIFTs not already owned - drives 993005 -> egmlos ->
        // dul[*].mlos); GetConstraints at a floor boundary (all flooridx==cn.f+1 entries);
        // GetEgogiftWithEnemyBuf.pool (== tfs[^1].egs). MASKED (proven RNG pool CONTENTS only, via
        // the [rt=...] array-segment mask): GetBattleRewardCase.pool (card levels),
        // GetEgogiftWithEnemyBuf.pool_v2/pool_v3 (enemy-buff subset + levels), GetEgogift.pool
        // (single theme gift). seq279/283 are per-seq masked (e==3 Prob-branch RNG gift pick).
        // cost/usedcost stay CEILING-masked and missions owner-excluded (unchanged from task-9).
        "/api/ExitMirrorDungeonMapNode",
        // cycle8 task-13b: UpdateMapNode is now byte-green. Task 10 made prevChoiceEvent +
        // dungeonUnitList + egmlos/mlos wire-correct; Task 11's deferred-gift queue fixed
        // currentEgoGifts (normal 9xxx gifts surface in the FOLLOWING ExitMapNode, not here);
        // Task 13's oid wire field closed egs[*].oid. The sole remaining divergence on all 37
        // records is `cels` (a per-account cumulative choice-pick history, e.g. counts 13/2/1) -
        // an OWNER-DESIGNATED account-state mask (below), same class as updated/synchronized/
        // missions: a fresh replay account cannot reproduce another account's lifetime choice log.
        "/api/UpdateMirrorDungeonMapNode",
        // cycle8 task-7: the dul wire model is now correct - Dungeonunitlist1 gained pord (-1)
        // and sid, ExitMapNode resets es[*].g to 0 and preserves server-tracked upidx (instead
        // of echoing the client's []), and the sibling Tfs wire type dropped the phantom `tid`
        // and gained upegs/ch. Those dul/tfs diffs are GONE from the replay. BUT these two
        // endpoints still can't be Covered: ExitMapNode returns the full currentInfo, which
        // diverges on non-dul lifecycle fields our handler never maintains (cost, pnids, rre
        // lifecycle, egs.oid, peids, etype, ess, missions.*), and UpdateMapNode returns the
        // EventManager's prevChoiceEvent + cels (we return empty) and must merge the request's
        // dungeonUnitList battle ch. Those are large non-dul ports - see task-7-report.md.
        // cycle8 task-12: shop-spend engine. cost/usedcost are DETERMINISTIC (injected
        // running cost/usedcost - a price DELTA), not the ExitMapNode ceiling formula.
        // TruthStateTracker now also tracks usedcost from partial shop responses (it
        // already tracked cost) so the injected prior cumulative spend is correct.
        //   - PurchaseEgoGiftMirrorDungeon (19/19 byte-match): the real bug was idx -
        //     it indexes only the "eg"-type slots, skipping any leading "up"/"upt"
        //     (personality-upgrade) slots in the same shop.slots array; MdEgoData's
        //     price table was already correct. usedcost is now the running cumulative
        //     shop-spend (save.currentInfo.usedcost += price), not the single op's price.
        //     egogifts[*].oid narrow-masked (COMBINE-fusion origin id, separate task).
        //   - RefreshShopEgoGiftsMirrorDungeon (10/10 byte-match): price is 15*rc (rc =
        //     1-based refresh count this floor, already a ShopInfo field) not a hardcoded
        //     flat 15; reroll is position-preserving (mutates each s==1 slot's id in place,
        //     s==0 slots untouched) - only shopInfo.slots[*].id is RNG-masked, slots[*].t
        //     and slots[*].s are deterministic and byte-verified, as are rc/fre/fkre/cf/
        //     aec/aesp.
        //   - UpgradeEgoGiftMirrorDungeon: NOT covered. The price-based UpgradeCost
        //     formula only matched 3/12 records; replaced with MdEgoData.TierUpgradeCost,
        //     a flat per-tier lookup from the static egoGiftUpgradeCostTable, which
        //     matches 11/12. The 12th (seq289, gift 9055) diverges 70 vs the table's 100
        //     with no derivable cause in this task's data (both 9055 and a same-tier gift
        //     that DID match came from the same battle-reward pull, ruling out a simple
        //     acquisition-channel rule) - not RNG, not oid, so per task-12-brief.md this is
        //     NOT masked. See task-12-report.md.
        "/api/PurchaseEgoGiftMirrorDungeon",
        "/api/RefreshShopEgoGiftsMirrorDungeon",
        // cycle8 task-13: fusion. AcquiredEgogifts gained the `oid` field (fusion origin id,
        // omit-when-null) so the "super" gifts 9992/9993 round-trip through the injected save
        // and echo with their oid intact - this also removed the Purchase egogifts[*].oid
        // mask, which is now byte-verified rather than masked. The handler now also echoes
        // starlightInfo (= save.currentInfo.slinfo). 4 of the 5 captured combines are fixed
        // recipes (deterministic, fully byte-verified); seq263 alone hits the random-roll
        // path (no fixed recipe for [9992,9993,9993]) - its rolled result id is per-seq masked
        // (see ReplayMasks.BySeq), everything else on that record still byte-verifies.
        "/api/CombineEgoGiftMirrorDungeon",
        // cycle8: UpgradeEgoGift now Covered. TierUpgradeCost byte-verifies cost/usedcost on 11/12
        // records; seq289 (fusion gift 9055, a -30% fusion-subsystem discount) is BySeq-masked - see
        // ReplayMasks. egoGift/dungeonUnitList byte-verify on all 12.
        "/api/UpgradeEgoGiftMirrorDungeon",
        // cycle8 task-14: EnterMapNode is now byte-green (77/77). nr is DERIVED (3 for
        // event/shop nodes e in {3,10}, 4 for battle-type nodes); passingNodeIds now echoes
        // save.currentInfo.pnids instead of a hardcoded empty list; changedHiddenNode is a new
        // field (always false in this capture); shopInfo is only generated + set on shop-node
        // (e==10) entry and omitted otherwise (was always regenerated before, clobbering the
        // tracked shop on every entry); ShopGiftCount's super-shop count is now 10 (was a wrong
        // 8). cels is OWNER-DESIGNATED account-state masked, same class as UpdateMapNode's.
        "/api/EnterMirrorDungeonMapNode",
        // cycle8: ReEnterMirrorDungeon just echoes the injected save verbatim (no mutation) -
        // byte-matches with zero masks (all 1 record, incl startdate, equals the injected
        // ground-truth from EnterMirrorDungeon).
        "/api/ReEnterMirrorDungeon",
        // cycle8: RecreateThemeFloorPool re-rolls the theme-floor pool for the NEXT floor. The
        // handler echoes the injected save with only tfps regenerated (MdMapGen.RecreateThemes:
        // 4 themes, idx = act number) and tfpsCreated bumped (injected + 1). All 19 records
        // byte-match once the RNG pool contents (tfps[*].tfid/egs/upegs) are masked; count (4),
        // idx (act), tfpsCreated, and every other saveInfo field are byte-verified.
        "/api/RecreateThemeFloorPoolMirrorDungeon",
        // cycle8: SelectThemeFloor generates the selected floor's map and starts the floor. The
        // procedural map (dungeonMap.ns) + its choiceEventList are irreducibly RNG and masked
        // (structure covered by the GenerateNewFloor unit test); usedcost is the same unknown
        // cost formula CEILING-masked on ExitMapNode; seq15's dul[*].l is account-roster state
        // (owned identity levels) masked per-record. Everything else byte-matches once the floor
        // index is taken from tfs.Count (leveladders is not the floor here), the party is
        // full-healed (ch->10000, cm cleared on revive), tfpsCreated resets to 0, eid is gated to
        // floors>=1, and startKeyword is no longer clobbered (preserved from AcquireStart).
        "/api/SelectThemeFloorMirrorDungeon",
        // cycle8: AcquireRewardEgoGifts (abno/hard-battle GetEgogift popup). Grants the picked
        // gift via the shared GrantEgoGift rule (an already-owned gift becomes the super 9992/
        // 9993 carrying the base as oid - seq84 9053->9993), and consumes ONLY the GetEgogift
        // entry, echoing the remaining reward events as remainRewardEvent. Battle-outcome stats
        // (statistics gd/rd, encounterstatistics) are masked - unreproducible like cost.
        "/api/AcquireRewardEgoGiftsMirrorDungeon",
        // cycle8: AcquireRewardEgoGiftsWithEnemyBuf (boss-clear reward). Grants the picked pool +
        // pool_v2 gifts (grouped: all pool first, then all pool_v2) via GrantEgoGift, consumes the
        // GetEgogiftWithEnemyBuf popup (keeping siblings), and rolls the next floor's 4-theme pool.
        // 7/10 records fully byte-verified; 3 per-record masked (seq106/191 undetermined trailing
        // GetEgogift trigger, seq241 fused-away Vestige). RNG theme contents + battle-derived
        // leveladders values masked; count/idx/super-oid byte-verified.
        "/api/AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon",
        // cycle8: AcquireMirrorDungeonBattleReward (encounter-reward-card pick). Consumes the
        // GetBattleRewardCase popup keeping all sibling reward events (the old handler replaced rre
        // wholesale AND wrongly re-rolled a theme pool - both fixed; tfps stays empty). rre
        // lifecycle byte-verified on all 12 records. Masked (all battle-outcome/RNG): cost, statistics,
        // encounterstatistics; the RNG reward-gift grant (egs on the 6 granting records) and the
        // STARLIGHT_MIN_MAX brcp roll (seq73) per-record.
        "/api/AcquireMirrorDungeonBattleReward",
        // cycle8: PurchaseUpgradePersonality spends module points to upgrade a unit's skill.
        // Consumes a shop slot (the pid-matching "up" slot, else the universal "upt" ticket, s 1->0),
        // appends the chosen idx to the unit's upidx, and charges price = upgradePersonality[idx].price
        // (from mirrordungeon-07-extreme.json) x (up ? 1 : 2 for the universal ticket). 10/14 records
        // byte-verify cost/usedcost exactly; the other 4 (seq183/214/234/260) carry a mid-run -30%
        // discount buff we do not model - their cost/usedcost are BySeq-masked (see ReplayMasks).
        // dungeonUnitList, shopInfo, and starlightInfo are byte-verified on all 14.
        "/api/PurchaseUpgradePersonalityMirrorDungeon",
        // cycle8: EnterExtremeMode/EnterInfiniteMode resume the run in a mode. They echo the injected
        // save with idx bumped by one (the mode-enter counter: EnterMD=0, SelectThemeFloor=1,
        // InfiniteMode=2, ExtremeMode=3 - byte-verified). statistics gd/rd + encounterstatistics are
        // stale battle-outcome totals (ExitMapNode never refreshes the tracked saveInfo.statistics) -
        // masked, same class as AcquireReward. Everything else byte-verifies.
        "/api/EnterExtremeMode",
        "/api/EnterInfiniteMode",
        // cycle8: ExitMirrorDungeon returns flat isEndDungeon/isclear = 1/1 (byte-verified) plus the
        // per-sinner battle-outcome statistics, which are masked (unreproducible battle totals).
        "/api/ExitMirrorDungeon",
        // cycle8: start-of-run flow (GetStartBuffFInfo -> SelectFormation -> EnableStartBuff ->
        // AcquireStartEgoGiftsAndCreateThemePool -> DetectMirrorDungeonEgogiftByStarlight), all
        // 5 byte-green on their one captured record each.
        //   - GetStartBuffFInfo: was a static empty MapPacket; now a real handler. The only
        //     captured call is pre-Enter (no save yet) so `enabled` is always []; the client
        //     wire type's `bufstate` field never appears in the capture and was dropped.
        //   - SelectFormation: es[*].idx now enumerates position (was hardcoded 0); spid is set
        //     to the 12 formation pids; startBufPoint/slinfo.pfb are now basePoint(60, static
        //     mirrordungeon-start-buffs-07.json) + detectThemeFloorDefaultPoint(20, static
        //     mirror-dungeon-common-data-md7.json) = 80 (was hardcoded 120). dul[*].g is the
        //     per-identity "gacksung" (awakening) investment - the same account-lifetime-state
        //     class as the l-mask below (a fresh replay account has no real per-identity
        //     investment) - derived via the same AccountDefaults.DerivePersonalities lookup as
        //     l, falling back to DefaultData's uniform default, and masked in the replay.
        //   - EnableStartBuff: was a static empty MapPacket; now spends the picked buffs' static
        //     cost (mirrordungeon-start-buffs-07.json) x remainPointToCostMultiplier, echoes
        //     sorted(buffids) as `enabled`, and (this run's only exercised branch,
        //     enableConvertedCost=true) converts the SelectFormation bonus remainder into
        //     slinfo.scc and zeroes startBufPoint. No route persists which buff ids got enabled
        //     into any currentInfo field (adding one would break every other endpoint that
        //     echoes currentInfo verbatim), so GetStartBuffFInfo can't reflect it post-enable -
        //     unexercised by the capture. TruthStateTracker gained a starlightInfo->slinfo merge
        //     + an EnableStartBuff-specific startBufPoint zero (its response carries no
        //     startBufPoint field to merge generically).
        //   - AcquireStartEgoGiftsAndCreateThemePool: sepsCreated now increments; startKeyword
        //     is looked up from the selected starter set's keyword before the seps catalog is
        //     cleared; slinfo.ieedt is set from the request's isEnableEgogiftDetectionToggle;
        //     efs.sbmlos is set to 3 (buff-106's ADDITIONAL_PERSONALITY_LEVEL_ON_ENTER_1ST_FLOOR,
        //     ponytail-hardcoded same as ExitMapNode's sbmlos cap-8 - no start-buff effect engine
        //     exists yet) and dul[*].mlos = sbmlos+egmlos for every unit. The theme pool's easy
        //     (idx 0) entries carry 3 reward egs and hard (idx 1) entries carry 4 (MdMapGen's
        //     BuildTfps was flat-4) - contents stay RNG-masked, only the count was wrong.
        //   - DetectMirrorDungeonEgogiftByStarlight: new route + new MirrorDungeonCurrentInfoResult
        //     wire type. Sets slinfo.degids = the request's egogiftIds, resets slinfo.ieedt to 0,
        //     and appends each requested id to egs as an AcquiredEgogifts if not already owned.
        "/api/GetStartBuffFInfoMirrorDungeon",
        "/api/SelectFormationMirrorDungeon",
        "/api/EnableStartBuffMirrorDungeon",
        "/api/AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon",
        "/api/DetectMirrorDungeonEgogiftByStarlight",
        // cycle8: PurchaseHealMirrorDungeon idx==1 (heal-all) is a genuine full heal - every
        // unit's ch -> 10000 (the 0-10000 max scale), not the old ch += 30 bandaid; cm += 15 was
        // already correct. usedcost is the cumulative shop-spend (save.currentInfo.usedcost +=
        // 100), not a flat 100. Byte-verified on the run's one record (seq313). idx==0
        // (single-unit heal) is unexercised by the capture and left as the old bandaid.
        "/api/PurchaseHealMirrorDungeon",
        // cycle8: AcquireMirrorDungeonConstraints consumes the GetConstraints reward popup
        // (same rt-remove pattern as AcquireRewardEgoGifts's GetEgogift) and records the pick as
        // a new scinfos entry `{flooridx: cn.f+1, ids}` for the floor about to start. The run's
        // one record (seq298) sends an empty selectIdxList (no constraint picked), so `ids` is
        // empty; a non-empty selection derives ids from the consumed pool by index but is
        // unexercised by the capture. Byte-verified: rre drops exactly the GetConstraints entry,
        // scinfos gains exactly the one new entry, everything else in currentInfo is untouched.
        "/api/AcquireMirrorDungeonConstraints",
        // cycle8: EnterMirrordungeonMapNodeBattleAfterChoice (no client packet - routed via the
        // PacketRouting.PacketId constant like SelectFormationMirrorDungeon). Stateless: builds
        // one abnormalityLogs entry per requested abno id, sorted by id, with `ps` populated
        // from the static abnormality-unit abnormalityPartList lookup (MdAbnoUnits). Byte-
        // verified on all 3 records (seq21/78/141, 9 abno ids total): outer length, each entry's
        // `id`, and each `ps[*].id`+count. RNG per-battle rolls (k/s/p) and resistance/attack
        // permutations (ps[*].atrr/atkr) are masked, same category as ExitMapNode's abno-log
        // masks.
        "/api/EnterMirrordungeonMapNodeBattleAfterChoice",
        // cycle8: MD exit-reward pair (see task-exitreward-report.md).
        "/api/PreviewMirrorDungeonExitReward",
        "/api/AcquireMirrorDungeonExitReward",
    };

    [SkippableFact]
    public async Task Replays_CoveredEndpoints_Match()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString);

        var name = $"replay_{Guid.NewGuid():N}";
        string jwt;
        using (var scope = factory.Services.CreateScope())
        {
            var store = new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>());
            await store.GetOrCreateByUsernameAsync(name);
            jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        }
        var client = factory.CreateClient();
        var failures = new List<string>();

        foreach (var (runId, file) in FixtureLoader.Runs)
        {
            var tracker = new TruthStateTracker();
            foreach (var rec in FixtureLoader.Records(file))
            {
                using (var scope = factory.Services.CreateScope())
                {
                    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var acc = ctx.Accounts.First(a => a.Username == name);
                    acc.MdSaveInfo = tracker.MdSaveInfoJson;
                    await ctx.SaveChangesAsync();
                }

                if (Covered.Contains(rec.Path) && rec.Req is not null)
                {
                    var req = rec.Req.DeepClone();
                    if (req["userAuth"] is JsonObject ua) ua["authCode"] = jwt;
                    var resp = await client.PostAsync(rec.Path, JsonContent.Create(req));
                    var ourJson = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
                    var diffs = JsonDiff.Compare(ourJson, rec.Res, ReplayMasks.For(runId, rec.Path, rec.Seq));
                    if (diffs.Count > 0)
                        failures.Add($"[{runId}] seq {rec.Seq} {rec.Path}: {string.Join(", ", diffs.Take(8))}");
                }

                tracker.Advance(rec.Path, rec.Res);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
