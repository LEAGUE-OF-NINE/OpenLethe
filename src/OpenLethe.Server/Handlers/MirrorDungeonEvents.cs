using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server.MirrorDungeon.Mapping;
using OpenLethe.Server.MirrorDungeon.Rules;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Port of server/src/api/md/update_mirror_dungeon_map_node.rs.
public static class MirrorDungeonEventsEndpoints
{
    public static IEndpointRouteBuilder MapMirrorDungeonEvents(this IEndpointRouteBuilder app)
    {
        var updateMapNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_UpdateMirrorDungeonMapNode>();

        app.MapPost("/api/UpdateMirrorDungeonMapNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var loaded = AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<UpdateMirrorDungeonMapNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            try
            {
                EventEngine.UpdateNode(run, p.currentnode, p.choiceEventData, p.dungeonUnitList);
            }
            catch (KeyNotFoundException)
            {
                return Results.StatusCode(500);
            }
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new UpdateMirrorDungeonMapNodeResult
            {
                prevChoiceEvent = save.currentInfo.pce,
                currentEgoGifts = save.currentInfo.egs,
                dungeonUnitList = save.currentInfo.dul,
            };
            return Results.Json(global::ResponsePacket<UpdateMirrorDungeonMapNodeResult>.Ok(result, updateMapNodeId), global::PacketJson.Options);
        });

        return app;
    }
}
