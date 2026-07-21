using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Ports of server/src/api/md/{acquire_start_ego_gifts_and_create_theme_pool,
/// recreate_theme_floor_pool,select_theme_floor,enter_mirror_dungeon_map_node,
/// exit_mirror_dungeon_map_node}_mirror_dungeon.rs, and the map/mod.rs helpers.
/// update_mirror_dungeon_map_node is DEFERRED (needs EventManager) - not implemented here.
public static class MirrorDungeonMapEndpoints
{
    // Port of map/mod.rs current_floor.
    public static long CurrentFloor(CurrentInfo ci) => ci.leveladders.Count;

    // Port of map/mod.rs theme_pack_id.
    public static long ThemePackId(CurrentInfo ci) => ci.tfs.Count > 0 ? ci.tfs[^1].tfid : 1001;

    // Port of map/mod.rs is_super_shop.
    public static bool? IsSuperShop(MirrorOriginSaveInfo save)
    {
        var node = save.dungeonMap.ns.FirstOrDefault(n => n.nid == save.currentInfo.cn.nid);
        if (node is null || node.e != 10) return null;
        return node.eid != 0;
    }

    // Port of map/mod.rs shop_gift_count.
    public static long ShopGiftCount(MirrorOriginSaveInfo save) => IsSuperShop(save) switch
    {
        true => 8,
        false => 5,
        null => 0,
    };

    // Port of exit_mirror_dungeon_map_node.rs get_enemy_buff_pool.
    private static List<long> EnemyBuffPool(long floor, int n)
    {
        List<long> pool = floor switch
        {
            1 => new() { 992211, 992212, 992213, 992214, 992215, 992216, 992217 },
            2 => new() { 992221, 992222, 992223, 992224, 992225, 992226, 992227, 992228 },
            3 => new() { 992231, 992232, 992233, 992234, 992235, 992236, 992237, 992238, 992239 },
            4 => new() { 992241, 992242, 992243, 992244, 992245, 992246, 992247, 992248 },
            _ => new() { 992201, 992202, 992203, 992204, 992205, 992206 },
        };
        return MdMapGen.ChooseMultiple(pool, n);
    }

    public static IEndpointRouteBuilder MapMirrorDungeonMap(this IEndpointRouteBuilder app)
    {
        var acquireStartId = global::PacketRouting.ResolvePacketId<global::ResPacket_AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon>();
        var recreateId = global::PacketRouting.ResolvePacketId<global::ResPacket_RecreateThemeFloorPoolMirrorDungeon>();
        var selectThemeFloorId = global::PacketRouting.ResolvePacketId<global::ResPacket_SelectThemeFloorMirrorDungeon>();
        var enterMapNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterMirrorDungeonMapNode>();
        var exitMapNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitMirrorDungeonMapNode>();

        app.MapPost("/api/AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            save.currentInfo.egs.AddRange(p.selectedEgoGiftIds.Select(id => new AcquiredEgogifts { id = id }));
            save.currentInfo.tfps = MdMapGen.PickThemes(CurrentFloor(save.currentInfo));
            save.currentInfo.seps = new();

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeonResult>.Ok(result, acquireStartId), global::PacketJson.Options);
        });

        app.MapPost("/api/RecreateThemeFloorPoolMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            // Params are unused by Rust too - read+discard just to 400 on an unparseable body.
            var p = await HandlerContext.ReadParamsAsync<object>(ctx);
            if (p is null) return Results.BadRequest();

            save.currentInfo.tfps = MdMapGen.PickThemes(CurrentFloor(save.currentInfo));
            save.currentInfo.seps = new();

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new RecreateThemeFloorPoolMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<RecreateThemeFloorPoolMirrorDungeonResult>.Ok(result, recreateId), global::PacketJson.Options);
        });

        app.MapPost("/api/SelectThemeFloorMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<SelectThemeFloorMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            try
            {
                MdMapGen.GenerateNewFloor(CurrentFloor(save.currentInfo), p.selectedThemeFoorId, save);
            }
            catch (KeyNotFoundException)
            {
                return Results.StatusCode(500);
            }

            save.idx = p.selectedIdx;

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new SelectThemeFloorMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<SelectThemeFloorMirrorDungeonResult>.Ok(result, selectThemeFloorId), global::PacketJson.Options);
        });

        app.MapPost("/api/EnterMirrorDungeonMapNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<EnterMirrorDungeonMapNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            save.currentInfo.cn = p.currentnode;
            save.currentInfo.shop.egpool = new MdThemePool().SelectRandomShopEgos(
                ThemePackId(save.currentInfo), (int)ShopGiftCount(save), CurrentFloor(save.currentInfo), null);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new EnterMirrorDungeonMapNodeResult
            {
                abnormalityLogs = new(),
                passingNodeIds = new(),
                currentNode = p.currentnode,
                shopInfo = save.currentInfo.shop,
                egogifts = save.currentInfo.egs,
                prevdul = new(),
                preves = new(),
                nr = 0,
                cost = save.currentInfo.cost,
            };
            return Results.Json(global::ResponsePacket<EnterMirrorDungeonMapNodeResult>.Ok(result, enterMapNodeId), global::PacketJson.Options);
        });

        app.MapPost("/api/ExitMirrorDungeonMapNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<ExitMirrorDungeonMapNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            save.currentInfo.cn = p.currentnode;
            save.currentInfo.dul = p.dungeonunitlist;

            // Some nodes don't send egoSkillStockList, such as rest stops.
            if (p.isupdatedEgoSkillStock == 1)
                save.currentInfo.ess = p.egoSkillStockList;

            save.currentInfo.nr = 1;
            save.currentInfo.pce.Clear();

            // Clear blocked purchased ego gifts from the shop.
            save.currentInfo.shop.peg.Clear();

            var matchingNode = save.dungeonMap.ns.FirstOrDefault(n => n.nid == p.currentnode.nid);
            if (matchingNode is null) return Results.BadRequest();

            save.currentInfo.eid = matchingNode.eid;
            var floor = CurrentFloor(save.currentInfo);

            // If the node "e" value is 6, move on to the next floor.
            if (matchingNode.e == 6)
            {
                var egoRewards = save.currentInfo.tfs.Count > 0 ? save.currentInfo.tfs[^1].egs : new List<long>();
                var levelupValues = new List<long>
                {
                    Random.Shared.Next(1, 3),
                    Random.Shared.Next(1, 3),
                    Random.Shared.Next(1, 3),
                    Random.Shared.Next(1, 3),
                };

                save.currentInfo.tfps = new()
                {
                    new Tfps { idx = 0, tfid = 1001, egs = new() { 9019, 9017, 9031, 9048 }, upegs = new() { 9051, 9048, 9001, 9066 } },
                };
                save.currentInfo.rre = new()
                {
                    new RemainRewardEvent
                    {
                        rt = "GetEgogiftWithEnemyBuf",
                        se = 2,
                        sh = 3,
                        pool = egoRewards,
                        pool_v2 = EnemyBuffPool(floor, egoRewards.Count),
                        pool_v3 = levelupValues,
                    },
                };
                foreach (var unit in save.currentInfo.dul) unit.ch = 10000;
            }

            // Hard abno battle.
            if (matchingNode.e == 14)
            {
                var rewards = MdAbRewards.GetByNodeId(matchingNode.eid);
                if (rewards is { Count: > 0 })
                {
                    var rewardEgos = rewards.Where(r => r.rewardType == "EGO_GIFT").Select(r => r.rewardId).ToList();
                    save.currentInfo.egs.AddRange(rewardEgos.Select(id => new AcquiredEgogifts { id = id }));
                    save.currentInfo.rre = rewardEgos.Select(id => new RemainRewardEvent
                    {
                        rt = "GetConfirmedEgogiftOnWinBattle",
                        se = 1,
                        sh = 1,
                        pool = new() { id },
                        pool_v2 = new(),
                        pool_v3 = new(),
                    }).ToList();
                }
            }

            // Abno battle or hard battle.
            if (matchingNode.e == 5 || matchingNode.e == 2)
            {
                save.currentInfo.rre = new()
                {
                    new RemainRewardEvent
                    {
                        rt = "GetEgogift",
                        se = 1,
                        sh = 1,
                        pool = new MdThemePool().SelectRandomEgosFromPool(ThemePackId(save.currentInfo), 1, floor),
                        pool_v2 = new(),
                        pool_v3 = new(),
                    },
                };
            }

            save.currentInfo.cost += MdCost.GetDefaultCost(matchingNode.e, floor);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new ExitMirrorDungeonMapNodeResult { currentInfo = save.currentInfo, abnormalityLogs = new() };
            return Results.Json(global::ResponsePacket<ExitMirrorDungeonMapNodeResult>.Ok(result, exitMapNodeId), global::PacketJson.Options);
        });

        return app;
    }
}
