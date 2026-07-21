using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
            var save = AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<UpdateMirrorDungeonMapNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            // Rust: pce.first().and_then(|e| e.nei).or_else(|| dungeonMap.ns node lookup).
            long? eid = null;
            var pe = save.currentInfo.pce.FirstOrDefault();
            if (pe is not null && pe.nei.HasValue) eid = pe.nei.Value;
            if (eid is null)
                eid = save.dungeonMap.ns.FirstOrDefault(n => n.nid == p.currentnode.nid)?.eid;
            if (eid is null) return Results.StatusCode(500);

            long choiceIdx = p.choiceEventData.sl.Count > 0 ? p.choiceEventData.sl[0] : 0;
            long cs = p.choiceEventData.cs;
            long next = MdEventManager.ProcessEvent(eid.Value, (int)choiceIdx, cs, new MdEventSave(save));

            save.currentInfo.pce.Insert(0, new ChoiceEventData { sl = new() { choiceIdx }, cs = -1, ri = 0, nei = next });

            account.MdSaveInfo = AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new UpdateMirrorDungeonMapNodeResult
            {
                prevChoiceEvent = new(),
                currentEgoGifts = save.currentInfo.egs,
                dungeonUnitList = save.currentInfo.dul,
            };
            return Results.Json(global::ResponsePacket<UpdateMirrorDungeonMapNodeResult>.Ok(result, updateMapNodeId), global::PacketJson.Options);
        });

        return app;
    }
}
