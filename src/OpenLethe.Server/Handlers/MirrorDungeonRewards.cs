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

        return app;
    }
}
