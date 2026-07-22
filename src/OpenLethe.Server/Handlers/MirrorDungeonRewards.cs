using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Ports of server/src/api/md/: combine_ego_gift, refresh_shop_ego_gifts,
/// get_mirror_dungeon_ego_gift_record, exit_mirror_dungeon.
public static class MirrorDungeonRewardsEndpoints
{
    public static IEndpointRouteBuilder MapMirrorDungeonRewards(this IEndpointRouteBuilder app)
    {
        var combineId = global::PacketRouting.ResolvePacketId<global::ResPacket_CombineEgoGiftMirrorDungeon>();
        var refreshId = global::PacketRouting.ResolvePacketId<global::ResPacket_RefreshShopEgoGiftsMirrorDungeon>();
        var recordId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetMirrorDungeonEgoGiftRecord>();
        var exitId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitMirrorDungeon>();
        var enemyBufId = global::PacketRouting.ResolvePacketId<global::ResPacket_AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon>();
        var battleRewardId = global::PacketRouting.ResolvePacketId<global::ResPacket_AcquireMirrorDungeonBattleReward>();

        app.MapPost("/api/CombineEgoGiftMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<CombineEgoGiftMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            // Remove the component egos - first match each, misses are ignored.
            foreach (var materialId in p.materialEgoGiftIds)
            {
                var pos = save.currentInfo.egs.FindIndex(e => e.id == materialId);
                if (pos >= 0) save.currentInfo.egs.RemoveAt(pos);
            }

            var isSuperShop = MirrorDungeonMapEndpoints.IsSuperShop(save) ?? false;
            var fused = MdEgoFusion.FuseGift(p.keyword, isSuperShop, p.materialEgoGiftIds);
            // Rust computes owned AFTER the removals.
            var owned = save.currentInfo.egs.Select(e => e.id).ToList();

            AcquiredEgogifts result;
            if (fused.FixedGiftId is { } fixedId)
            {
                result = new AcquiredEgogifts { id = fixedId };
            }
            else
            {
                var themePool = new MdThemePool();
                var candidates = themePool
                    .ShopEgoGiftsPool(MirrorDungeonMapEndpoints.ThemePackId(save.currentInfo))
                    .Where(g => MdEgoData.DetermineEgoTier(g) == fused.Tier && !owned.Contains(g))
                    .ToList();

                // Rust compares Option<String> == Option<String>: when the keyword roll
                // failed (null), gifts with a null keyword match. Same semantics here.
                var keywordMatches = candidates
                    .Where(g => MdEgoData.GetById(g)?.keyword == fused.Keyword)
                    .ToList();
                if (keywordMatches.Count > 0) candidates = keywordMatches;

                // ponytail: 9034 is Rust's hard-coded fallback when nothing is eligible.
                result = new AcquiredEgogifts
                {
                    id = candidates.Count > 0 ? candidates[System.Random.Shared.Next(candidates.Count)] : 9034,
                };
            }

            save.currentInfo.egs.Add(result);
            account.MdSaveInfo = AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var body = new CombineEgoGiftMirrorDungeonResult
            {
                resultEgoGift = result,
                resultEgoGifts = new List<AcquiredEgogifts> { result },
                isSuccess = true,
                egoGifts = save.currentInfo.egs,
                dungeonUnitList = save.currentInfo.dul,
            };
            return Results.Json(global::ResponsePacket<CombineEgoGiftMirrorDungeonResult>.Ok(body, combineId), global::PacketJson.Options);
        });

        app.MapPost("/api/RefreshShopEgoGiftsMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<RefreshShopEgoGiftsMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var bought = save.currentInfo.shop.peg.Count;
            // Rust: usize subtraction (would panic on underflow) then clamp at 30. A
            // negative count here just makes the selection loops no-op.
            var count = (int)MirrorDungeonMapEndpoints.ShopGiftCount(save) - bought;
            if (count > 30) count = 30;

            var randomGifts = new MdThemePool().SelectRandomShopEgos(
                MirrorDungeonMapEndpoints.ThemePackId(save.currentInfo),
                count,
                MirrorDungeonMapEndpoints.CurrentFloor(save.currentInfo),
                p.keyword);

            save.currentInfo.shop.egpool = save.currentInfo.shop.peg.Concat(randomGifts).ToList();
            save.currentInfo.cost -= 15; // Rust hard-codes the refresh cost.

            account.MdSaveInfo = AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var body = new RefreshShopEgoGiftsMirrorDungeonResult
            {
                cost = save.currentInfo.cost,
                shopInfo = save.currentInfo.shop,
                usedcost = 15,
            };
            return Results.Json(global::ResponsePacket<RefreshShopEgoGiftsMirrorDungeonResult>.Ok(body, refreshId), global::PacketJson.Options);
        });

        app.MapPost("/api/GetMirrorDungeonEgoGiftRecord", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();

            var body = new GetMirrorDungeonEgoGiftRecordResult
            {
                acquiredegogifts = MdEgoData.AllIds(),
                themeFloorIds = new MdThemePool().pools.Values.Select(t => t.id).ToList(),
            };
            return Results.Json(global::ResponsePacket<GetMirrorDungeonEgoGiftRecordResult>.Ok(body, recordId), global::PacketJson.Options);
        });

        app.MapPost("/api/ExitMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();

            // Rust returns a flat (1, 1, []) with no save mutation.
            var body = new ExitMirrorDungeonResult { isEndDungeon = 1, isclear = 1 };
            return Results.Json(global::ResponsePacket<ExitMirrorDungeonResult>.Ok(body, exitId), global::PacketJson.Options);
        });

        app.MapPost("/api/AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<AcquireRewardEgoGiftsWithEnemyBufParams>(ctx);
            if (p is null) return Results.BadRequest();

            // leveladders also drives the per-floor unit level-ups.
            save.currentInfo.leveladders.Add(3);

            var popup = save.currentInfo.rre.FirstOrDefault(re => re.rt == "GetEgogiftWithEnemyBuf");
            if (popup is not null)
            {
                foreach (var index in p.selectIndexList)
                {
                    // Rust unwrap_or(&0): a missing index still appends an id-0 gift.
                    var i = (int)index;
                    save.currentInfo.egs.Add(new AcquiredEgogifts { id = i >= 0 && i < popup.pool.Count ? popup.pool[i] : 0 });
                    save.currentInfo.egs.Add(new AcquiredEgogifts { id = i >= 0 && i < popup.pool_v2.Count ? popup.pool_v2[i] : 0 });
                }
            }

            var candidates = MdEncounterCard.PickRandomEncounterCards(
                MirrorDungeonMapEndpoints.CurrentFloor(save.currentInfo) + 2);
            var cards = ChooseMultiple(candidates, 3);

            save.currentInfo.rre = new List<RemainRewardEvent>
            {
                new() { rt = "GetBattleRewardCase", se = 1, sh = 2, pool = cards },
            };

            account.MdSaveInfo = AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            // Intentional 3-vs-1 mismatch: leveladders gets 3 (above) but the response
            // reports 1. Byte-for-byte faithful to Rust's
            // acquire_reward_ego_gifts_with_enemy_buf_mirror_dungeon.rs, which pushes 3 and
            // responds vec![1]. Do not "fix" this to match.
            var body = new AcquireRewardEgoGiftsWithEnemyBufResult
            {
                dungeonUnitList = save.currentInfo.dul,
                levelAdders = new List<long> { 1 },
                saveinfo = save,
            };
            return Results.Json(global::ResponsePacket<AcquireRewardEgoGiftsWithEnemyBufResult>.Ok(body, enemyBufId), global::PacketJson.Options);
        });

        app.MapPost("/api/AcquireMirrorDungeonBattleReward", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<AcquireMirrorDungeonBattleRewardParams>(ctx);
            if (p is null) return Results.BadRequest();

            var cards = save.currentInfo.rre
                .Where(re => re.rt == "GetBattleRewardCase")
                .SelectMany(re => re.pool)
                .ToList();

            var newRewards = new List<RemainRewardEvent>();
            foreach (var index in p.selectIndexList)
            {
                var i = (int)index;
                if (i < 0 || i >= cards.Count) continue; // Rust filter_map drops misses
                if (!MdEncounterCard.EncounterRewardMap.TryGetValue(cards[i], out var reward)) continue;
                var rp = reward.rewardParams;
                if (rp is null) continue;

                switch (reward.rewardType)
                {
                    case "COST_EGOGIFT_START_CATEGORY":
                        save.currentInfo.cost += RollCost(rp);
                        if (RewardRandomEgoGift(rp.egoGiftAcquirableProb ?? 0, rp.egoGiftTierRange) is { } startEgo)
                        {
                            save.currentInfo.egs.Add(new AcquiredEgogifts { id = startEgo });
                            newRewards.Add(new RemainRewardEvent
                            {
                                rt = "GetConfirmedEgogiftOnWinBattle",
                                se = 1,
                                sh = 1,
                                pool = new List<long> { startEgo },
                            });
                        }
                        break;

                    case "COST":
                        save.currentInfo.cost += RollCost(rp);
                        break;

                    case "EGOGIFT":
                        // Note: unlike the COST_EGOGIFT arm, Rust adds NO rre here.
                        if (RewardRandomEgoGift(rp.egoGiftAcquirableProb ?? 0, rp.egoGiftTierRange) is { } ego)
                            save.currentInfo.egs.Add(new AcquiredEgogifts { id = ego });
                        break;

                    case "EGOSTOCK":
                        if (rp.leastEgoStock is not { } least) break; // randomEgoStock is ignored upstream
                        var stock = new Dictionary<string, long>
                        {
                            ["CR"] = 0, ["SC"] = 0, ["AM"] = 0, ["SH"] = 0,
                            ["AZ"] = 0, ["IN"] = 0, ["VI"] = 0,
                        };
                        foreach (var ess in save.currentInfo.ess) stock[ess.t] = ess.n;

                        // ponytail: Rust sorts a Vec built from a HashMap, so ties break
                        // randomly there; OrderBy over the seeded dict is deterministic here.
                        foreach (var key in stock.OrderBy(kv => kv.Value).Take((int)least.kind).Select(kv => kv.Key).ToList())
                            stock[key] += least.num;

                        save.currentInfo.ess = stock.Select(kv => new EgoSkillStock { t = kv.Key, n = kv.Value }).ToList();
                        break;
                }
            }

            save.currentInfo.tfps = MdMapGen.PickThemes(MirrorDungeonMapEndpoints.CurrentFloor(save.currentInfo));
            save.currentInfo.rre = newRewards;

            account.MdSaveInfo = AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var body = new AcquireMirrorDungeonBattleRewardResult { saveinfo = save };
            return Results.Json(global::ResponsePacket<AcquireMirrorDungeonBattleRewardResult>.Ok(body, battleRewardId), global::PacketJson.Options);
        });

        return app;
    }

    private static long RollCost(MdRewardParams rp) =>
        System.Random.Shared.NextInt64(rp.acquireCostMin ?? 0, (rp.acquireCostMax ?? 0) + 1);

    /// Rust choose_multiple: a random sample of up to n items, order arbitrary.
    private static List<long> ChooseMultiple(List<long> source, int n)
    {
        var copy = new List<long>(source);
        for (var i = copy.Count - 1; i > 0; i--)
        {
            var j = System.Random.Shared.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy.Take(n).ToList();
    }

    /// Port of acquire_mirror_dungeon_battle_reward.rs reward_random_ego_gift.
    /// ponytail: UPSTREAM QUIRK, PRESERVED - Rust looks for the drop pool with
    /// dungeonId == 5, but the only shipped file is mirrordungeon-egogift-droppool-7.json
    /// (dungeonId 7). So this always returns null on real data and EGOGIFT /
    /// COST_EGOGIFT_START_CATEGORY rewards never grant a gift. Do not "fix" it to the
    /// max-dungeon-id lookup MdThemePool uses - that is a different code path. The full
    /// roll below stays ported so the behaviour is right if a dungeon-5 pool ever ships.
    private static long? RewardRandomEgoGift(double acquirableProb, MdTierRange? tierRange)
    {
        var pool = MdThemeData.DropPools().FirstOrDefault(p => p.dungeonId == 5);
        if (pool is null || tierRange is null) return null;
        if (System.Random.Shared.NextDouble() > acquirableProb) return null;

        var egos = pool.egoGifts
            .Where(id => tierRange.WithinRange(MdEgoData.DetermineEgoTier(id) ?? 0))
            .ToList();
        return egos.Count == 0 ? null : egos[System.Random.Shared.Next(egos.Count)];
    }
}
