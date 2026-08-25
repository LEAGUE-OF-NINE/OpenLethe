using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Mapping;
using OpenLethe.Server.MirrorDungeon.Rules;
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
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<CombineEgoGiftMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            FusionRules.Combine(run, p.materialEgoGiftIds, p.keyword, p.isOrigin);
            var save = WireMapper.ToWire(run);
            // FusionRules.Combine always appends the fused result last.
            var resultWire = save.currentInfo.egs[^1];

            account.MdSaveInfo = AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var body = new CombineEgoGiftMirrorDungeonResult
            {
                resultEgoGift = resultWire,
                resultEgoGifts = new List<AcquiredEgogifts> { resultWire },
                isSuccess = true,
                egoGifts = save.currentInfo.egs,
                dungeonUnitList = save.currentInfo.dul,
                starlightInfo = save.currentInfo.slinfo,
            };
            return Results.Json(global::ResponsePacket<CombineEgoGiftMirrorDungeonResult>.Ok(body, combineId), global::PacketJson.Options);
        });

        app.MapPost("/api/RefreshShopEgoGiftsMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<RefreshShopEgoGiftsMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            ShopRules.RefreshShop(run);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var body = new RefreshShopEgoGiftsMirrorDungeonResult
            {
                cost = save.currentInfo.cost,
                shopInfo = save.currentInfo.shop,
                usedcost = save.currentInfo.usedcost,
            };
            return Results.Json(global::ResponsePacket<RefreshShopEgoGiftsMirrorDungeonResult>.Ok(body, refreshId), global::PacketJson.Options);
        });

        app.MapPost("/api/GetMirrorDungeonEgoGiftRecord", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
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
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();

            // isEndDungeon/isclear are flat 1/1. statistics is the per-sinner battle-outcome tally
            // (gd/rd, and even which sinners participated) accumulated over real battles the replay
            // never runs - echoed from the save but masked as unreproducible (same class as the
            // EnterExtremeMode/AcquireReward statistics masks).
            var loaded = AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            // Hidden save side effect (not part of THIS endpoint's own response, only surfaces
            // via the following Preview/Acquire's saveInfo echo - capture-confirmed
            // md-extreme seq319 -> seq321's saveInfo.isEndDungeon flips 0 -> 1 here).
            MirrorOriginSaveInfo? save = null;
            if (loaded is not null)
            {
                var run = WireMapper.ToDomain(loaded);
                RewardResolution.ExitMirrorDungeon(run);
                save = WireMapper.ToWire(run);
                account.MdSaveInfo = AccountFields.Set(save);
                await HandlerContext.SaveAsync(ctx);
            }
            var body = new ExitMirrorDungeonResult
            {
                isEndDungeon = 1,
                isclear = 1,
                statistics = save?.statistics ?? new(),
            };
            return Results.Json(global::ResponsePacket<ExitMirrorDungeonResult>.Ok(body, exitId), global::PacketJson.Options);
        });

        app.MapPost("/api/AcquireRewardEgoGiftsWithEnemyBufMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<AcquireRewardEgoGiftsWithEnemyBufParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            RewardResolution.AcquireWithEnemyBuf(run, p.selectIndexList);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            // egoGifts + levelAdders echo the full accumulated lists (verified: response.egoGifts
            // == currentInfo.egs, response.levelAdders == currentInfo.leveladders). remainRewardEvent
            // is whatever reward events remain after consuming GetEgogiftWithEnemyBuf.
            var body = new AcquireRewardEgoGiftsWithEnemyBufResult
            {
                egoGifts = save.currentInfo.egs,
                remainRewardEvent = save.currentInfo.rre,
                dungeonUnitList = save.currentInfo.dul,
                levelAdders = save.currentInfo.leveladders,
                saveinfo = save,
            };
            return Results.Json(global::ResponsePacket<AcquireRewardEgoGiftsWithEnemyBufResult>.Ok(body, enemyBufId), global::PacketJson.Options);
        });

        app.MapPost("/api/AcquireMirrorDungeonBattleReward", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<AcquireMirrorDungeonBattleRewardParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            RewardResolution.AcquireBattleReward(run, p.selectIndexList);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var body = new AcquireMirrorDungeonBattleRewardResult { saveinfo = save };
            return Results.Json(global::ResponsePacket<AcquireMirrorDungeonBattleRewardResult>.Ok(body, battleRewardId), global::PacketJson.Options);
        });

        return app;
    }
}
