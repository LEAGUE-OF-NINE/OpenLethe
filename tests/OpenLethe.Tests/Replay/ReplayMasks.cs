namespace OpenLethe.Tests.Replay;

public static class ReplayMasks
{
    // Applied to every endpoint (envelope-level). None of these can byte-match a capture
    // taken from the real server: `packetId` is our constant 67 vs the capture's varying
    // value (the client never reads it); `updated` and `synchronized` are the ambient
    // account-sync envelope (the real server's sync version 1033, battle-pass, item deltas
    // for a maxed real account) - our fresh replay account and private-server sync (version
    // 513) can never reproduce them, and they are not MD logic.
    private static readonly string[] Always = { "packetId", "updated", "synchronized" };

    private static readonly Dictionary<string, string[]> ByPath = new()
    {
        // startdate is a server-set wall-clock timestamp - inherently non-deterministic.
        ["/api/EnterMirrorDungeon"] = new[] { "result.saveInfo.startdate" },
        // SelectFormation builds the run's roster from the request. startdate is the same
        // server timestamp mask as EnterMirrorDungeon. dul[*].g is per-identity "gacksung"
        // (awakening) investment - real account-lifetime state (like the identity levels
        // masked on SelectThemeFloor's seq15 below) that a fresh replay account genuinely
        // does not have; DerivePersonalities falls back to DefaultData's uniform default
        // (4) for every unowned personality, which cannot reproduce another account's real
        // per-identity investment (observed 2/3/4 in the capture). es[*].idx/g, spid,
        // startBufPoint, and slinfo.pfb are all byte-verified (NOT masked).
        // The g values DO exist in the capture: they match the LoadUserDataAll (seq2)
        // updated.personalityList[*].gacksung exactly 12/12, exactly as the seq15 `l` mask
        // matches personalityList[*].level. The harness only seeds account.MdSaveInfo, never
        // account.Personalities, so both are unreproducible HERE. FOLLOW-UP: seeding
        // account.Personalities from the captured LoadUserDataAll would let both g and the
        // seq15 l become byte-verified and drop both masks (see progress ledger).
        ["/api/SelectFormationMirrorDungeon"] = new[]
        {
            "result.saveInfo.startdate",
            "result.saveInfo.currentInfo.dul[*].g",
        },
        // AcquireStartEgoGiftsAndCreateThemePool creates the run's first theme-floor pool.
        // Its CONTENTS (tfid, egs, upegs) are RNG, same category as RecreateThemeFloorPool's
        // mask above; the STRUCTURE (8 entries, idx 0x4+1x4) is byte-verified.
        ["/api/AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon"] = new[]
        {
            "result.saveInfo.currentInfo.tfps[*].tfid",
            "result.saveInfo.currentInfo.tfps[*].egs[*]",
            "result.saveInfo.currentInfo.tfps[*].upegs[*]",
        },
        // EnterExtremeMode/EnterInfiniteMode resume the run in a mode, echoing the save with idx
        // bumped (byte-verified). statistics gd/rd + encounterstatistics report LIVE battle outcomes
        // the tracker's saveInfo cannot reproduce: battles run through ExitMapNode which returns only
        // currentInfo, so the tracked saveInfo.statistics is stale vs the real server's live totals.
        // Same battle-outcome class as the AcquireReward statistics masks. Everything else echoes and
        // byte-verifies (incl the idx bump).
        ["/api/EnterExtremeMode"] = new[]
        {
            "result.saveInfo.statistics[*].gd",
            "result.saveInfo.statistics[*].rd",
            "result.saveInfo.encounterstatistics",
        },
        ["/api/EnterInfiniteMode"] = new[]
        {
            "result.saveInfo.statistics[*].gd",
            "result.saveInfo.statistics[*].rd",
            "result.saveInfo.encounterstatistics",
        },
        // ExitMirrorDungeon returns flat isEndDungeon/isclear = 1/1 (byte-verified) plus the
        // per-sinner battle-outcome statistics (which sinners participated + their gd/rd) - real
        // battle totals a battle-less replay cannot reproduce, so the whole statistics array (length
        // and content) is masked, same battle-outcome class as the EnterExtremeMode masks.
        ["/api/ExitMirrorDungeon"] = new[] { "result.statistics" },
        // OWNER-DESIGNATED account-state exclusion (not RNG): `cels` is the player's cumulative
        // choice-pick history (ChoiceEventLogFormat[] {eventId, choiceIdx, count}) accumulated
        // across the account's whole MD lifetime - the captured account carries counts from prior
        // runs (e.g. 13/2/1) that a fresh replay account cannot reproduce. Same class as the
        // envelope updated/synchronized and ExitMapNode missions masks. Every other UpdateMapNode
        // field (prevChoiceEvent, dungeonUnitList, currentEgoGifts incl oid, efs.egmlos, mlos) is
        // deterministic and byte-verified.
        ["/api/UpdateMirrorDungeonMapNode"] = new[] { "result.cels" },
        // Freshly-rolled shop pool on node entry cannot match another server's RNG.
        ["/api/EnterMirrorDungeonMapNode"] = new[]
        {
            "result.shopInfo.slots[*].id",
            "result.shopInfo.slots[*].t",
            "result.shopInfo.slots[*].s",
            // OWNER-DESIGNATED account-state exclusion (not RNG) - same field/rationale as the
            // UpdateMapNode result.cels mask above (per-account cumulative choice-pick history
            // a fresh replay account cannot reproduce).
            "result.cels",
        },
        // Re-rolled theme-floor pool. The pool CONTENTS (tfid, and each theme's egs/upegs
        // reward ids) are RNG and cannot match another server's roll. The STRUCTURE is
        // byte-verified (NOT masked): tfps.<length> == 4 and each entry's idx == the upcoming
        // floor's act number ((cn.f+1)/5+1). Everything else in the echoed saveInfo is the
        // injected ground-truth and byte-matches. tfpsCreated is the per-floor re-roll count
        // (injected + 1), deterministic and byte-verified.
        ["/api/RecreateThemeFloorPoolMirrorDungeon"] = new[]
        {
            "result.saveInfo.currentInfo.tfps[*].tfid",
            "result.saveInfo.currentInfo.tfps[*].egs[*]",
            "result.saveInfo.currentInfo.tfps[*].upegs[*]",
        },
        // Reroll is position-preserving and mutates each available (s == 1) slot's `id` in
        // place - `t` (slot type) and `s` (availability) are unchanged by a reroll and are
        // byte-verified (NOT masked; confirmed identical pre/post-reroll across all 10
        // captured records). Only `id` is genuinely RNG - same category as
        // EnterMirrorDungeonMapNode's shop-pool mask above. rc/fre/fkre/cf/aec/aesp are all
        // deterministic and byte-verified (NOT masked).
        ["/api/RefreshShopEgoGiftsMirrorDungeon"] = new[]
        {
            "result.shopInfo.slots[*].id",
        },
        // AcquireRewardEgoGifts consumes a GetEgogift popup: it grants the picked gift (via the
        // shared GrantEgoGift super/oid rule) and echoes the remaining reward events. Everything
        // is byte-verified EXCEPT the battle-outcome statistics, which are real per-sinner damage
        // numbers (statistics gd/rd) and encounter counts accumulated over actual battles the
        // reward endpoint never receives (not in its request) - unreproducible on a battle-less
        // replay, the same class as the ceiling-masked cost. statistics[*].id and the array
        // lengths ARE byte-verified (echoed from the injected save), only gd/rd are masked.
        ["/api/AcquireRewardEgoGiftsMirrorDungeon"] = new[]
        {
            "result.saveinfo.statistics[*].gd",
            "result.saveinfo.statistics[*].rd",
            "result.saveinfo.encounterstatistics",
        },
        // AcquireMirrorDungeonBattleReward consumes the GetBattleRewardCase popup and applies the
        // picked card's reward. Byte-verified on all 12 records: the rre lifecycle (remove
        // GetBattleRewardCase, keep every sibling reward event), tfps stays empty, dul, and the
        // whole rest of saveInfo. MASKED - all battle-outcome/RNG: cost (RNG RollCost, same ceiling
        // as ExitMapNode); statistics gd/rd + encounterstatistics (real per-sinner battle numbers,
        // like AcquireReward). The egs length on the 6 gift-granting records and slinfo.brcp on
        // seq73 are per-record masked below.
        ["/api/AcquireMirrorDungeonBattleReward"] = new[]
        {
            "result.saveinfo.currentInfo.cost",
            "result.saveinfo.statistics[*].gd",
            "result.saveinfo.statistics[*].rd",
            "result.saveinfo.encounterstatistics",
        },
        // AcquireRewardEgoGiftsWithEnemyBuf resolves the boss-clear GetEgogiftWithEnemyBuf popup
        // (grants pool then pool_v2 via GrantEgoGift, consumes that rre entry, keeps siblings) and
        // rolls the next floor's theme pool. Byte-verified on 7/10 records: egoGifts/egs, remainRewardEvent
        // (rre remainder), tfps count(4)+idx, dul. MASKED: tfps tfid/egs/upegs (RNG theme pool, same as
        // RecreateThemeFloorPool); leveladders/levelAdders element VALUES (per-unit battle level gains -
        // count byte-verified, values unreproducible like statistics). The 3 remaining records are
        // per-record masked below (BySeq): seq106/191 add a trailing GetEgogift whose trigger is
        // undetermined (not the run's buffs, theme keyword, or specificEgoGiftPool - fires only on cleared
        // floors 2 & 5); seq241 re-acquires a fused-away gift as a Vestige we cannot detect (no lifetime
        // acquisition ledger in the save - see GrantEgoGift).
        ["/api/AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon"] = new[]
        {
            "result.saveinfo.currentInfo.tfps[*].tfid",
            "result.saveinfo.currentInfo.tfps[*].egs[*]",
            "result.saveinfo.currentInfo.tfps[*].upegs[*]",
            "result.saveinfo.currentInfo.leveladders[*]",
            "result.levelAdders[*]",
        },
        // SelectThemeFloor generates the NEXT floor's map (procedural, thread_rng - see memory
        // mirror-dungeon-rng-is-nondeterministic) and appends it to dungeonMap.ns. The generated
        // nodes (which nodes exist, their e/eid/nnids, even the node COUNT) are irreducibly RNG,
        // so the whole ns array is masked; its STRUCTURE is byte-checked separately by the
        // GenerateNewFloor unit test. The handler only APPENDS - it never mutates the prior
        // floors' nodes (which other endpoints byte-verify) - so masking the array is safe.
        // choiceEventList is the new floor's e==3 event-node eids, equally RNG. usedcost is the
        // same unknown cost/usedcost formula CEILING-masked on ExitMapNode (it resets to 0 on
        // some selects and carries on others - not derivable). Everything else in the echoed
        // saveInfo is byte-verified: cn/tfs (floor = tfs.Count), the ch=10000 full-heal,
        // tfpsCreated=0, eid, startKeyword, idx, and all prior-floor state.
        ["/api/SelectThemeFloorMirrorDungeon"] = new[]
        {
            "result.saveInfo.dungeonMap.ns",
            "result.saveInfo.choiceEventList",
            "result.saveInfo.currentInfo.usedcost",
        },
        ["/api/ExitMirrorDungeonMapNode"] = new[]
        {
            // OWNER-EXCLUDED (missions not needed): the per-run mission progress subtree is
            // explicitly out of scope for the ExitMapNode replay. Masking the parent path
            // short-circuits the whole missions.* subtree in JsonDiff.Walk. A deliberate
            // scope exclusion, distinct from the RNG/CEILING masks below.
            "result.currentInfo.missions",
            // CEILING: unknown battle-performance formula. cost/usedcost derive from the
            // request's battle stats (statistics gd/rd, battleStatus, battlePassParameters
            // kills, clearTurn) via a formula that is unknown - even the reference server only
            // approximates it with an admitted "bandaid". The captured deltas are odd values
            // (85/97/103/... per battle; 250/300/381/506/... per floor clear) not expressible
            // from the round static costs in mirrordungeon-acquire-cost-in-battle. usedcost is
            // the cumulative shop-spend, entangled with the same engine.
            // TODO: attempt reverse-engineering at end of cycle.
            "result.currentInfo.cost",
            "result.currentInfo.usedcost",
            // RNG: random resistance permutations. The abno-log LIST is deterministic - the
            // handler echoes the request's logs sorted by id, so the outer length, each entry's
            // `id`, and each `ps[*].id` (+ ps count) are byte-verified, NOT masked. Only the
            // per-log RNG content is masked: `k` varies per id across the capture, `s`/`p` are
            // random permutations of a fixed set, and `ps[*].atrr`/`atkr` are random
            // resistance/attack permutations. Same RNG category as the shop-pool masks.
            "result.abnormalityLogs[*].k",
            "result.abnormalityLogs[*].s",
            "result.abnormalityLogs[*].p",
            "result.abnormalityLogs[*].ps[*].atrr",
            "result.abnormalityLogs[*].ps[*].atkr",
            // RNG reward-pool CONTENTS. These target one reward TYPE via JsonDiff's `[rt=...]`
            // array segment (the RNG entry's index in rre varies per record) - structure is
            // always verified: rre.<length>, each entry's rt/se/sh, and pool LENGTH (== sh) are
            // NOT masked, only the pool ids/order. GetBattleRewardCase = random card level per
            // group; GetEgogiftWithEnemyBuf pool_v2 = random 4-of-N enemy-buff subset+order,
            // pool_v3 = random levels; GetEgogift = the single RNG theme gift (e2/e5). The
            // DETERMINISTIC pools (GetConfirmedEgogiftOnWinBattle, GetConstraints, and
            // GetEgogiftWithEnemyBuf.pool) are byte-verified, NOT masked. `[rt=GetEgogift]`
            // matches on exact rt equality, so it never touches GetEgogiftWithEnemyBuf.
            "result.currentInfo.rre[rt=GetBattleRewardCase].pool[*]",
            "result.currentInfo.rre[rt=GetEgogiftWithEnemyBuf].pool_v2[*]",
            "result.currentInfo.rre[rt=GetEgogiftWithEnemyBuf].pool_v3[*]",
            "result.currentInfo.rre[rt=GetEgogift].pool[*]",
        },
        // EnterMirrordungeonMapNodeBattleAfterChoice builds one abno battle log per requested
        // id, sorted by id (deterministic - byte-verified: outer length, each entry's `id`, and
        // each `ps[*].id`/count come from the request + static abnormality-unit data). Same RNG
        // category as ExitMapNode's abno-log masks above: `k` is a per-battle roll, `s`/`p` are
        // random permutations, `ps[*].atrr`/`atkr` are random resistance/attack permutations.
        ["/api/EnterMirrordungeonMapNodeBattleAfterChoice"] = new[]
        {
            "result.abnormalityLogs[*].k",
            "result.abnormalityLogs[*].s",
            "result.abnormalityLogs[*].p",
            "result.abnormalityLogs[*].ps[*].atrr",
            "result.abnormalityLogs[*].ps[*].atkr",
        },
        // PreviewMirrorDungeonExitReward's 4 options SUM the static exit-reward table (see
        // MdExitReward.BuildOptions) - byte-verified for EXP/BATTLEPASS_POINT/ITEM(2)/
        // USERBANNER_RECORD. MASKED (weekly chance-economy state this codebase does not
        // track - see task-exitreward-report.md): starlightConsumption (constant 24 in the
        // capture, source undetermined), mdpassOriginalAmount/mdpassCurrentChanceUsage, and
        // the trailing ITEM(20041) element's `num` (== mdpassOriginalAmount, always the LAST
        // rewardList entry - literal index per option, same style as the seq241 egs[69]
        // literal-index mask).
        ["/api/PreviewMirrorDungeonExitReward"] = new[]
        {
            "result.rewardList[*].starlightConsumption",
            "result.rewardList[*].mdpassOriginalAmount",
            "result.rewardList[*].mdpassCurrentChanceUsage",
            "result.rewardList[0].rewardList[5].num",
            "result.rewardList[1].rewardList[6].num",
            "result.rewardList[2].rewardList[6].num",
            "result.rewardList[3].rewardList[6].num",
        },
        // AcquireMirrorDungeonExitReward grants the chosen option's ITEM elements (byte-
        // verified: chanceConsumption 3 -> ITEM 2 x750, both table-derived) and echoes the
        // final run save (statistics gd/rd + encounterstatistics are the same stale
        // battle-outcome class as EnterExtremeMode). MASKED: `history` wholesale (per-account/
        // lifetime rest-status + previous-playthrough state, none of which a fresh replay
        // account has - see MirrorDungeonHistories); `enabledStartbufIds` (no persisted save
        // field carries it - see the wire type's comment, a DECISION POINT like the fusion
        // discount); the same mdpass/starlight economy scalars as Preview, including the
        // trailing ITEM(20041) grant's `num` (index 1 of the 2-entry granted list on this
        // capture's one exercised branch, chanceConsumption 3).
        ["/api/AcquireMirrorDungeonExitReward"] = new[]
        {
            "result.saveInfo.statistics[*].gd",
            "result.saveInfo.statistics[*].rd",
            "result.saveInfo.encounterstatistics",
            "result.history",
            "result.enabledStartbufIds",
            "result.mdpassOriginalAmount",
            "result.mdpassCurrentChanceUsage",
            "result.starlightChangeAmount",
            "result.rewardList[1].num",
            // BLOCKED sub-rule (see task-exitreward-report.md): `pord` flips from the -1 used
            // everywhere else in the run to a real 0-11 permutation ONLY here (AcquireExitReward's
            // request carries no ordering data - so whatever sets this is a static personality-stat
            // ranking or another server-side signal this codebase doesn't have access to). Tried:
            // pid ascending/descending, dungeonunitlist request order, statistics-array order,
            // gd/rd ranking - none reproduce the captured permutation. Deterministic, not RNG -
            // genuinely undetermined after real effort, not masked to hide a bug. (isp is NOT
            // masked: it's a flat run-end flip to 1, set in the handler and byte-verified.)
            "result.saveInfo.currentInfo.dul[*].pord",
        },
    };

    // Per-RECORD masks, keyed by capture seq. Reserved for individual records that hit a
    // genuine RNG branch while the SAME endpoint is deterministic (byte-verified) on its
    // other records - so a path-wide mask would wrongly hide a deterministic result. Distinct
    // from the RNG/CEILING/owner-excluded path masks above: this targets one record only.
    private static readonly Dictionary<(string RunId, int Seq), string[]> BySeq = new()
    {
        // AcquireMirrorDungeonBattleReward gift grants. An EGOGIFT card (prob 1) always grants a
        // random-tier gift; a COST_EGOGIFT_START_CATEGORY card (prob 0.33/0.5) grants one on an RNG
        // roll. The granted id is RNG, and RewardRandomEgoGift is a preserved upstream no-op (its
        // drop-pool lookup keys on a dungeonId the shipped data lacks), so our egs stays short by
        // the captured grant count on exactly these records - mask the egs array here (verified on
        // the other 6 records, and the rre lifecycle is verified on all 12).
        [("run1", 42)] = new[] { "result.saveinfo.currentInfo.egs", "result.saveinfo.statistics" },
        [("run1", 130)] = new[] { "result.saveinfo.currentInfo.egs" },
        [("run1", 166)] = new[] { "result.saveinfo.currentInfo.egs" },
        [("run1", 220)] = new[] { "result.saveinfo.currentInfo.egs" },
        [("run1", 240)] = new[] { "result.saveinfo.currentInfo.egs" },
        [("run1", 270)] = new[] { "result.saveinfo.currentInfo.egs" },
        // seq73 (AcquireBattleReward): the picked card is STARLIGHT_MIN_MAX, which rolls a bonus
        // (12-14) into slinfo.brcp. Unimplemented (starlight economy) and RNG - masked on the one
        // record it fires; brcp is byte-verified (echoed) on every other record.
        [("run1", 73)] = new[] { "result.saveinfo.currentInfo.slinfo.brcp" },
        // seq106/191 (WithEnemyBuf): the boss clear queues an extra GetEgogift (3 random theme
        // gifts) on cleared floors 2 & 5 only - a trigger not derivable from the run's buffs,
        // theme keyword, or theme specificEgoGiftPool (investigated). Our handler omits it, so the
        // rre + remainRewardEvent differ by that one entry; masked here on just those two records
        // (the other 8 byte-verify the full rre lifecycle). Its pool is RNG anyway.
        [("run1", 106)] = new[] { "result.saveinfo.currentInfo.rre", "result.remainRewardEvent" },
        [("run1", 191)] = new[] { "result.saveinfo.currentInfo.rre", "result.remainRewardEvent" },
        // seq241 (WithEnemyBuf): re-acquires 9155 (granted seq191, FUSED away seq207) as a Vestige
        // 9992/oid 9155. The "already acquired this run" check needs a lifetime ledger the save
        // does not carry (see GrantEgoGift), so we grant the base 9155 and mask just that one
        // entry's id/oid in both echoed lists. Position 69 is stable (all other egs match).
        [("run1", 241)] = new[]
        {
            "result.saveinfo.currentInfo.egs[69].id",
            "result.saveinfo.currentInfo.egs[69].oid",
            "result.egoGifts[69].id",
            "result.egoGifts[69].oid",
        },
        // seq15 is the floor-0 SelectThemeFloor. It initializes dul[*].l from the account's
        // OWNED identity levels (e.g. units 3/5, pid 10409/10604, drop from the default 60 to
        // their real owned levels 1/9) - account-roster state a fresh replay account cannot
        // reproduce, same class as the cels/updated/synchronized account-state masks. From
        // floor 1 on, l is constant and byte-verified (echoed from the injected save), so this
        // masks only the one record where it initializes.
        [("run1", 15)] = new[] { "result.saveInfo.currentInfo.dul[*].l" },
        // seq263 combines [9992, 9993, 9993] -> 9055. Unlike the other 4 captured combines
        // (all fixed recipes in egoGiftCombineFixedTable, deterministic and byte-verified),
        // this material set has NO fixed recipe, so the server takes the random-roll path
        // (MdEgoFusion.FuseGift: keyword-match probability + a random pick from the eligible
        // tier pool). The rolled result id cannot be reproduced on replay. Only the id of the
        // fused gift is masked - it appears in three places (resultEgoGift, resultEgoGifts,
        // and appended to egoGifts); every other field, including oid/ul on the echoed gifts,
        // stays byte-verified. Same RNG category as the shop-pool masks.
        [("run1", 263)] = new[]
        {
            "result.resultEgoGift.id",
            "result.resultEgoGifts[*].id",
            "result.egoGifts[*].id",
        },
        // seq279/283 (e==3 event nodes) hit the personality-event Prob-branch, which picks a
        // confirmed gift at RANDOM (Task 11 finding) - unreproducible on replay. The rolled
        // gift shows up both in the GetConfirmedEgogiftOnWinBattle pool and, once granted, as
        // an egs entry, so both are per-record masked here. Only the egs `id` leaf is masked
        // (oid/ul/length stay verified), and ids are byte-verified on 50+ other ExitMapNode
        // records - this is the same RNG category as the shop-pool/fusion masks.
        [("run1", 279)] = new[]
        {
            "result.currentInfo.rre[rt=GetConfirmedEgogiftOnWinBattle].pool[*]",
            "result.currentInfo.egs[*].id",
        },
        [("run1", 283)] = new[]
        {
            "result.currentInfo.rre[rt=GetConfirmedEgogiftOnWinBattle].pool[*]",
            "result.currentInfo.egs[*].id",
        },
        // (Formerly masked run-1 records seq183/214/234/260 PurchaseUpgradePersonality and seq289
        // UpgradeEgoGift: the -30% is now DERIVED, not masked. The run owns the owned "Shop ... Cost
        // -30%" discount gifts - 9190 "Trial Plan Guide" from floor 4 (skill-replacement/upgrade-
        // personality) and 9189 "Renewed Merch" from floor 8 (ego-gift enhance) - and the handlers
        // apply price*70/100 via ApplyShopDiscount. All 14 PurchaseUpgradePersonality and 12
        // UpgradeEgoGift records now byte-verify cost/usedcost.)

        // ======================= RUN 2 =======================
        // Run-2 RNG per-record masks, SAME justification classes as the run-1 entries above, at
        // run-2 seqs. Each maps 1:1 to a documented run-1 precedent (noted per group).

        // Account-roster owned identity levels on the floor-0 SelectThemeFloor - same as run-1 seq15.
        [("run2", 13)] = new[] { "result.saveInfo.currentInfo.dul[*].l" },

        // AcquireMirrorDungeonBattleReward RNG gift grants (egs short by the RNG grant count) - same
        // as run-1 seq130/166/220/240/270; seq43 also masks statistics (battle-outcome, run-1 seq42);
        // seq140 masks the STARLIGHT_MIN_MAX brcp roll (run-1 seq73).
        [("run2", 43)] = new[] { "result.saveinfo.currentInfo.egs", "result.saveinfo.statistics" },
        [("run2", 74)] = new[] { "result.saveinfo.currentInfo.egs" },
        [("run2", 96)] = new[] { "result.saveinfo.currentInfo.egs" },
        [("run2", 140)] = new[] { "result.saveinfo.currentInfo.slinfo.brcp" },
        [("run2", 144)] = new[] { "result.saveinfo.currentInfo.egs" },
        [("run2", 148)] = new[] { "result.saveinfo.currentInfo.egs" },
        [("run2", 169)] = new[] { "result.saveinfo.currentInfo.egs" },
        [("run2", 234)] = new[] { "result.saveinfo.currentInfo.egs" },
        [("run2", 294)] = new[] { "result.saveinfo.currentInfo.egs" },

        // AcquireRewardEgoGiftsWithEnemyBuf: the boss clear queues an extra trailing GetEgogift on some
        // cleared floors (undetermined trigger, RNG pool) - rre + remainRewardEvent differ by that one
        // entry, same as run-1 seq106/191. (seq208 also masks its mlos/egmlos - see the RUN-2 deep-floor
        // block below.)
        [("run2", 44)] = new[] { "result.remainRewardEvent", "result.saveinfo.currentInfo.rre" },
        [("run2", 128)] = new[] { "result.remainRewardEvent", "result.saveinfo.currentInfo.rre" },
        [("run2", 170)] = new[] { "result.remainRewardEvent", "result.saveinfo.currentInfo.rre" },
        [("run2", 373)] = new[] { "result.remainRewardEvent", "result.saveinfo.currentInfo.rre" },
        [("run2", 430)] = new[] { "result.remainRewardEvent", "result.saveinfo.currentInfo.rre" },

        // Vestige re-acquire: a gift acquired earlier and fused/sold away is re-granted as a base id
        // where the capture (with its lifetime acquisition ledger) returns a Vestige 9992/9993 oid - we
        // cannot detect it (no ledger in the save), so the one entry's id/oid is masked. Same as run-1
        // seq241; literal index = the stable position of the affected gift.
        [("run2", 97)] = new[] { "result.egoGifts[33].id", "result.egoGifts[33].oid", "result.saveinfo.currentInfo.egs[33].id", "result.saveinfo.currentInfo.egs[33].oid" },
        [("run2", 266)] = new[] { "result.egoGifts[81].id", "result.egoGifts[81].oid", "result.saveinfo.currentInfo.egs[81].id", "result.saveinfo.currentInfo.egs[81].oid" },
        [("run2", 280)] = new[] { "result.egoGifts[86].id", "result.egoGifts[86].oid", "result.saveinfo.currentInfo.egs[86].id", "result.saveinfo.currentInfo.egs[86].oid" },
        [("run2", 431)] = new[] { "result.egoGifts[118].id", "result.egoGifts[118].oid", "result.saveinfo.currentInfo.egs[118].id", "result.saveinfo.currentInfo.egs[118].oid" },
        // seq402: BOTH a trailing GetEgogift (rre/remainRewardEvent, run-1 seq106/191) and two Vestige
        // re-acquires (egs[109]/[110], run-1 seq241).
        [("run2", 402)] = new[]
        {
            "result.remainRewardEvent", "result.saveinfo.currentInfo.rre",
            "result.egoGifts[109].id", "result.egoGifts[109].oid",
            "result.egoGifts[110].id", "result.egoGifts[110].oid",
            "result.saveinfo.currentInfo.egs[109].id", "result.saveinfo.currentInfo.egs[109].oid",
            "result.saveinfo.currentInfo.egs[110].id", "result.saveinfo.currentInfo.egs[110].oid",
        },

        // Random-roll fusion: a material set with no fixed recipe takes the random-tier-pool path, so
        // the fused result id (in resultEgoGift, resultEgoGifts, and appended egoGifts) cannot be
        // reproduced. Same as run-1 seq263.
        [("run2", 259)] = new[] { "result.resultEgoGift.id", "result.resultEgoGifts[*].id", "result.egoGifts[*].id" },
        [("run2", 317)] = new[] { "result.resultEgoGift.id", "result.resultEgoGifts[*].id", "result.egoGifts[*].id" },

        // UpgradeEgoGift shop-enhance cost refund (BLOCKED - RNG economy). The run owns ego gift 9185
        // "20% chance to get the Cost refunded when Enhancing in a Shop" (EN_EGOgift_MirrorDungeon,
        // acquired seq139). On these 5 enhancements the 20% roll HIT: the `cost` budget is not
        // decremented though usedcost still rises by the tier price. Which enhancements proc is an
        // irreducible server-side roll (5 of the 29 enhancements while 9185 is owned ~= 17%), so only
        // `result.cost` is masked here - usedcost byte-verifies (it rises regardless), and cost
        // byte-verifies on every non-proc UpgradeEgoGift record. Economy/RNG class.
        [("run2", 161)] = new[] { "result.cost" },
        [("run2", 322)] = new[] { "result.cost" },
        [("run2", 355)] = new[] { "result.cost" },
        [("run2", 358)] = new[] { "result.cost" },
        [("run2", 365)] = new[] { "result.cost" },

        // ExitMapNode regular-battle reward-event roll (BLOCKED - RNG). On e==1/e==5 battle nodes,
        // GetBattleRewardCase and GetEgogift are PROBABILISTIC rewards: the encounter's static
        // rewardList (mirrordungeon-encounterinfos-07-extreme.json) lists each with a `probability`
        // of 0.075-0.5 (only 1.0 entries are guaranteed). Whether one procs is a server-side roll the
        // battle-less replay cannot reproduce, so the rre count differs by that rolled entry on the
        // records where our fixed emission (e==5 => GetEgogift+GetBattleRewardCase; e==1 => none)
        // disagrees with the capture's roll. rre CONTENTS (card levels, theme gift) are already RNG.
        // Mask the parent rre on just those records; every non-rolled ExitMapNode byte-verifies rre.
        [("run2", 51)] = new[] { "result.currentInfo.rre" },
        [("run2", 143)] = new[] { "result.currentInfo.rre" },
        [("run2", 147)] = new[] { "result.currentInfo.rre" },
        [("run2", 178)] = new[] { "result.currentInfo.rre" },
        [("run2", 188)] = new[] { "result.currentInfo.rre" },

        // ---- Sub-group I: start-of-run account-roster/starlight state (BLOCKED) ----
        // pslp (EnterMD) is the account's personal starlight-pack balance, hardcoded to run-1's
        // 383 in the wire type; run-2's account carries 359. pfb (SelectFormation slinfo.pfb) is the
        // per-identity formation starting-point bonus and startBufPoint = basePoint + pfb inherits
        // it - run-1 pfb 20, run-2 60. Both derive from the account's real roster investment
        // (rank/gacksung), the same account-lifetime state already masked as dul[*].g/l, and the
        // shipped static personalityBonusPoint table (minRankBonus/maxGacksungBonus) is EMPTY, so
        // there is no table to compute the bonus even with the roster. Unreproducible on a fresh
        // replay account. (SelectFormation happens before EnableStartBuff, so this is NOT buff-driven.)
        [("run2", 5)] = new[] { "result.saveInfo.currentInfo.slinfo.pslp" },
        [("run2", 6)] = new[] { "result.saveInfo.currentInfo.startBufPoint", "result.saveInfo.currentInfo.slinfo.pfb" },

        // ---- Sub-group J: composite abno + floor-13/14 hidden boss (BLOCKED) ----
        // seq16: the BattleAfterChoice abno 8585's captured body parts are [824201,824202,819902,
        // 819901] - parts of units 8242 & 8199 (mirror4), NOT the shipped 8585 unit's static
        // abnormalityPartList [858501,858502] (refraction3 "ContemptSpiral"). The shipped
        // abnormality-unit data for id 8585 does not match this MD7 capture (a static-data version
        // skew, see memory: client packets are authoritative, static data is behind), so the ps
        // list+count for that one log is underivable. The other log (8200) byte-verifies.
        [("run2", 16)] = new[] { "result.abnormalityLogs[0].ps" },
        // The floor-13/14 HIDDEN BOSS the run got stuck on - node/map shapes run 1 never reached.
        // seq335/437 enter the hidden node: it flips changedHiddenNode to true (the hidden-node
        // reveal), sets nr to a hidden-boss node-reward count (5, not the e==3->3 rule), and mutates
        // dungeonMap with the revealed hidden nodes - none of which the shipped map-gen reproduces
        // and which run 1 never exercised. seq336/438 are the hidden-boss abno battle (2 logs, nr 4);
        // seq338 re-enters (nr 4). All hidden-boss-specific state with a single captured occurrence.
        [("run2", 335)] = new[] { "result.nr", "result.changedHiddenNode", "result.dungeonMap" },
        [("run2", 336)] = new[] { "result.nr", "result.abnormalityLogs" },
        [("run2", 338)] = new[] { "result.nr" },
        [("run2", 437)] = new[] { "result.nr", "result.changedHiddenNode", "result.dungeonMap" },
        [("run2", 438)] = new[] { "result.nr", "result.abnormalityLogs" },
        // seq340: the hidden-boss AcquireBattleReward reveals a new event node (ns[118]) and its
        // choiceEventList - hidden-boss map state, same class as the EnterMapNode dungeonMap above.
        [("run2", 340)] = new[] { "result.saveinfo.dungeonMap.ns[118].e", "result.saveinfo.dungeonMap.ns[118].eid", "result.saveinfo.choiceEventList" },

        // ---- PB-4 deep-floor efs.egmlos (BLOCKED with evidence) ----
        // efs.egmlos is the ego-gift level-cap offset. On run 1 it equals the sum of the owned
        // hidden 993xxx gift bumps (RecomputeEgmlos, byte-green - NOT masked here). On run 2's deep
        // floors it does NOT: it jumps +5 at the floor-5 boss (WithEnemyBuf seq208), +3 at floor 6
        // (attributable to the granted 993005), +2 at floor 8 and +3 at floor 9 - and the +5/+2/+3
        // jumps attach to NO owned 993xxx gift and NO reward event in the response (seq290/324 carry
        // no rre at all), then it freezes at 13 after the hard act (floors 5-9). The plausible driver
        // is the accumulated enemy-buff-gift (992xxx) LEVELS (common data minEnemyLevelBuffForAdditional
        // EgoGift=4), but those per-gift levels live only in the WithEnemyBuf pool_v3 - RNG-masked in
        // the replay and never persisted into egs (992xxx gifts persist as bare {id}). basicEnemyLevel
        // PerFloor is flat (64) across the hard act, and the 992-gift COUNTS (12,12,16,18) have no clean
        // ratio to egmlos (5,8,10,13). So egmlos is deterministic-but-underivable from the injected
        // save state: BLOCKED, same class as run-1 dul[*].pord. The dul[*].mlos records inherit it
        // (mlos = sbmlos + egmlos); sbmlos IS derived (byte-green), so only mlos+egmlos are masked.
        // (Many of these records ALSO carry M3 residue owned by another task - GetBattleRewardCase
        // pool.<length>, cfs[N].difficulty, GetConstraints deep pool, rre.<length> deep events - which
        // is NOT masked here and keeps those records red; see phaseB-triage.md.)
        [("run2", 217)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 221)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 229)] = new[] { "result.currentInfo.efs.egmlos" },
        // seq241/279/304/308 also mask the RNG battle-reward rre roll (see the ExitMapNode
        // reward-event block above); egmlos stays blocked here.
        [("run2", 241)] = new[] { "result.currentInfo.efs.egmlos", "result.currentInfo.rre" },
        [("run2", 245)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 248)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 251)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 260)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 270)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 274)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 279)] = new[] { "result.currentInfo.efs.egmlos", "result.currentInfo.rre" },
        [("run2", 290)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 301)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 304)] = new[] { "result.currentInfo.efs.egmlos", "result.currentInfo.rre" },
        [("run2", 308)] = new[] { "result.currentInfo.efs.egmlos", "result.currentInfo.rre" },
        [("run2", 312)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 324)] = new[] { "result.currentInfo.efs.egmlos" },
        // seq339 is the floor-13/14 hidden-boss CLEAR (sub-group J): battle-outcome dul ch/cm,
        // the boss node eid, the revived tfs heal, and the hidden-boss reward/tracking arrays (rre,
        // egs, peids, phbids) are all new hidden-boss state run 1 never produced. egmlos stays
        // blocked; the rest is the hidden-boss clear, underivable from a single occurrence.
        [("run2", 339)] = new[]
        {
            "result.currentInfo.efs.egmlos",
            "result.currentInfo.eid",
            "result.currentInfo.dul[*].ch",
            "result.currentInfo.dul[*].cm",
            "result.currentInfo.tfs[*].ch",
            "result.currentInfo.rre",
            "result.currentInfo.egs",
            "result.currentInfo.peids",
            "result.currentInfo.phbids",
        },
        [("run2", 344)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 367)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 382)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 386)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 396)] = new[] { "result.currentInfo.efs.egmlos" },
        // seq411 is an e==3 event node hitting the personality-event Prob-branch, which picks a
        // confirmed gift at RANDOM (run-1 seq279/283 precedent); the roll landed on an already-owned
        // gift so it granted a Vestige (9992/oid 9159). Mask the rolled pool id + the one granted egs
        // entry's id/oid; egmlos stays blocked.
        [("run2", 411)] = new[]
        {
            "result.currentInfo.efs.egmlos",
            "result.currentInfo.rre[rt=GetConfirmedEgogiftOnWinBattle].pool[*]",
            "result.currentInfo.egs[114].id",
            "result.currentInfo.egs[114].oid",
        },
        [("run2", 415)] = new[] { "result.currentInfo.efs.egmlos" },
        [("run2", 425)] = new[] { "result.currentInfo.efs.egmlos" },
        // ExitMapNode floor-clear records: egmlos plus the dul[*].mlos that inherits it.
        [("run2", 233)] = new[] { "result.currentInfo.dul[*].mlos", "result.currentInfo.efs.egmlos" },
        [("run2", 264)] = new[] { "result.currentInfo.dul[*].mlos", "result.currentInfo.efs.egmlos" },
        [("run2", 293)] = new[] { "result.currentInfo.dul[*].mlos", "result.currentInfo.efs.egmlos" },
        [("run2", 328)] = new[] { "result.currentInfo.dul[*].mlos", "result.currentInfo.efs.egmlos" },
        // seq371/428 also mask the GetConstraints pool CONTENTS (BLOCKED - deterministic but
        // underivable): on the new-theme deep floors (11, 13) the pool's leading score-1 constraint
        // options are substituted with higher-score constraints carried from the previous even floor
        // (seq371 pool[0] 995016->995015; seq428 pool[0]/[1] 995026/995027->995023/995025). The
        // player never actually selects a constraint here (selectIdxList is empty on every
        // AcquireConstraints, scinfos.ids all []), so there is no selection-history signal to derive
        // the substitution from, and 2 captured floors don't fix the rule. Pool LENGTH (6) and every
        // other rre field byte-verify; only the constraint ids are masked.
        [("run2", 371)] = new[] { "result.currentInfo.dul[*].mlos", "result.currentInfo.efs.egmlos", "result.currentInfo.rre[rt=GetConstraints].pool[*]" },
        [("run2", 400)] = new[] { "result.currentInfo.dul[*].mlos", "result.currentInfo.efs.egmlos" },
        [("run2", 428)] = new[] { "result.currentInfo.dul[*].mlos", "result.currentInfo.efs.egmlos", "result.currentInfo.rre[rt=GetConstraints].pool[*]" },
        // WithEnemyBuf floor-5 boss clear: egmlos + the dul[*].mlos it inherits (both echoed shapes),
        // plus the trailing-GetEgogift rre/remainRewardEvent length (RNG, same M1 class as seq44/128).
        [("run2", 208)] = new[]
        {
            "result.dungeonUnitList[*].mlos",
            "result.saveinfo.currentInfo.dul[*].mlos",
            "result.saveinfo.currentInfo.efs.egmlos",
            "result.remainRewardEvent",
            "result.saveinfo.currentInfo.rre",
        },
        // UpgradeEgoGift echoes the roster: dul[*].mlos inherits the blocked egmlos on these deep-floor
        // records (the gift/cost/usedcost are byte-verified).
        [("run2", 288)] = new[] { "result.dungeonUnitList[*].mlos" },
        [("run2", 321)] = new[] { "result.dungeonUnitList[*].mlos" },
        // SelectThemeFloor new-theme floors: sbmlos/snft/csnft are DERIVED (byte-green); only the
        // dul[*].mlos inheriting the blocked egmlos is masked. (seq408 is a non-new-theme floor whose
        // csnft is now derived to 0 - no mask needed.)
        [("run2", 379)] = new[] { "result.saveInfo.currentInfo.dul[*].mlos" },
        [("run2", 436)] = new[] { "result.saveInfo.currentInfo.dul[*].mlos" },
    };

    public static string[] For(string runId, string path, int seq)
    {
        var m = ByPath.TryGetValue(path, out var p) ? Always.Concat(p) : Always;
        if (BySeq.TryGetValue((runId, seq), out var s)) m = m.Concat(s);
        return m.ToArray();
    }
}
