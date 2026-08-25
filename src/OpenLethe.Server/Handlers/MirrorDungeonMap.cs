using OpenLethe.Server.MirrorDungeon.Mapping;
using OpenLethe.Server.MirrorDungeon.Rules;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Ports of server/src/api/md/{acquire_start_ego_gifts_and_create_theme_pool,
/// recreate_theme_floor_pool,select_theme_floor,enter_mirror_dungeon_map_node,
/// exit_mirror_dungeon_map_node}_mirror_dungeon.rs, and the map/mod.rs helpers.
/// update_mirror_dungeon_map_node is DEFERRED (needs EventManager) - not implemented here.
public static class MirrorDungeonMapEndpoints
{
    public static IEndpointRouteBuilder MapMirrorDungeonMap(this IEndpointRouteBuilder app)
    {
        var acquireStartId = global::PacketRouting.ResolvePacketId<global::ResPacket_AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon>();
        var recreateId = global::PacketRouting.ResolvePacketId<global::ResPacket_RecreateThemeFloorPoolMirrorDungeon>();
        var selectThemeFloorId = global::PacketRouting.ResolvePacketId<global::ResPacket_SelectThemeFloorMirrorDungeon>();
        var enterMapNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterMirrorDungeonMapNode>();
        var exitMapNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitMirrorDungeonMapNode>();

        app.MapPost("/api/AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            StartRunRules.AcquireStartAndCreateThemePool(run, p.selectedSetId, p.selectedEgoGiftIds, p.isEnableEgogiftDetectionToggle);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<AcquireStartEgoGiftsAndCreateThemePoolMirrorDungeonResult>.Ok(result, acquireStartId), global::PacketJson.Options);
        });

        app.MapPost("/api/RecreateThemeFloorPoolMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            // Params are unused by Rust too - read+discard just to 400 on an unparseable body.
            var p = await HandlerContext.ReadParamsAsync<object>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            MapGenerator.RecreateThemePool(run);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new RecreateThemeFloorPoolMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<RecreateThemeFloorPoolMirrorDungeonResult>.Ok(result, recreateId), global::PacketJson.Options);
        });

        app.MapPost("/api/SelectThemeFloorMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<SelectThemeFloorMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            try
            {
                MapGenerator.GenerateFloor(run, p.selectedThemeFoorId, p.selectedIdx);
            }
            catch (KeyNotFoundException)
            {
                return Results.StatusCode(500);
            }
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new SelectThemeFloorMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<SelectThemeFloorMirrorDungeonResult>.Ok(result, selectThemeFloorId), global::PacketJson.Options);
        });

        app.MapPost("/api/EnterMirrorDungeonMapNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<EnterMirrorDungeonMapNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            var enteredNode = run.Floor.Nodes.FirstOrDefault(n => n.Nid == p.currentnode.nid);
            if (enteredNode is null) return Results.BadRequest();
            var isShopNode = enteredNode.E == 10;
            var (changedHiddenNode, nr) = MapNodeRules.EnterNode(run, p.currentnode);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new EnterMirrorDungeonMapNodeResult
            {
                abnormalityLogs = new(),
                // Echo the accumulated passed-node tracker (ExitMapNode is the only writer).
                passingNodeIds = save.currentInfo.pnids,
                currentNode = p.currentnode,
                // Only present when entering a shop node (e==10) - omitted (null) on every other
                // node type, matching the wire type's WhenWritingNull shopInfo contract.
                shopInfo = isShopNode ? save.currentInfo.shop : null,
                egogifts = save.currentInfo.egs,
                prevdul = new(),
                preves = new(),
                nr = nr,
                cels = save.currentInfo.cels,
                cost = save.currentInfo.cost,
                changedHiddenNode = changedHiddenNode,
            };
            return Results.Json(global::ResponsePacket<EnterMirrorDungeonMapNodeResult>.Ok(result, enterMapNodeId), global::PacketJson.Options);
        });

        // No client packet exists for this route (see StaticRoutes.cs) - resolve the packetId
        // constant, same as SelectFormationMirrorDungeon. Stateless: builds one battle log per
        // requested abnormality id, sorted by id, from static abnormality-unit data - nothing
        // in the save is read or mutated (capture-verified: currentInfo is unchanged across
        // this call on all 3 records).
        app.MapPost("/api/EnterMirrordungeonMapNodeBattleAfterChoice", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<EnterMirrordungeonMapNodeBattleAfterChoiceParams>(ctx);
            if (p is null) return Results.BadRequest();

            var logs = EventEngine.BattleAfterChoice(p.abnormalityids);

            var result = new EnterMirrordungeonMapNodeBattleAfterChoiceResult { abnormalityLogs = logs };
            return Results.Json(global::ResponsePacket<EnterMirrordungeonMapNodeBattleAfterChoiceResult>.Ok(result, global::PacketRouting.PacketId), global::PacketJson.Options);
        });

        app.MapPost("/api/ExitMirrorDungeonMapNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<ExitMirrorDungeonMapNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            var matchingNode = run.Floor.Nodes.FirstOrDefault(n => n.Nid == p.currentnode.nid);
            if (matchingNode is null) return Results.BadRequest();

            var exitResult = RewardEngine.ResolveNodeExit(
                run, p.currentnode, p.dungeonunitlist, p.noderesult, p.choiceEventData,
                p.isupdatedEgoSkillStock == 1, p.egoSkillStockList, p.abnormalityLogs);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new ExitMirrorDungeonMapNodeResult { currentInfo = save.currentInfo, abnormalityLogs = exitResult.AbnormalityLogs };
            return Results.Json(global::ResponsePacket<ExitMirrorDungeonMapNodeResult>.Ok(result, exitMapNodeId), global::PacketJson.Options);
        });

        return app;
    }
}
