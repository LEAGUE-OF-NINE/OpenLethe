using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Ports of server/src/api/storydungeon/*.rs (the 6 non-EventManager handlers).
/// Save state lives in Account.StorySaveInfo as a server Wire StorySaveInfo.
public static class StoryDungeonEndpoints
{
    public static IEndpointRouteBuilder MapStoryDungeon(this IEndpointRouteBuilder app)
    {
        var enterId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterStoryDungeon>();
        app.MapPost("/api/EnterStoryDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<EnterStoryDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var map = OpenLethe.Server.StoryMapData.GetStoryMapById(p.stageid);
            var firstNode = map?.floors.Count > 0 && map.floors[0].sectors.Count > 0 && map.floors[0].sectors[0].nodes.Count > 0
                ? map.floors[0].sectors[0].nodes[0] : null;
            if (firstNode is null || !long.TryParse(firstNode.id, out var startId))
                return Results.StatusCode(500);

            var save = new StorySaveInfo
            {
                dungeonid = p.stageid,
                currentinfo = new Currentinfo
                {
                    dul = p.personalities,
                    cn = new Currentnode { f = 0, s = 0, nid = startId },
                    scpn = new Currentnode { f = 0, s = 0, nid = startId },
                },
            };
            account.StorySaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var result = new EnterStoryDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<EnterStoryDungeonResult>.Ok(result, enterId), global::PacketJson.Options);
        });

        var reEnterId = global::PacketRouting.ResolvePacketId<global::ResPacket_ReEnterStoryDungeon>();
        app.MapPost("/api/ReEnterStoryDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<StorySaveInfo>(account.StorySaveInfo) ?? new StorySaveInfo();
            var result = new ReEnterStoryDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<ReEnterStoryDungeonResult>.Ok(result, reEnterId), global::PacketJson.Options);
        });

        var enterNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterStoryDungeonMapNode>();
        app.MapPost("/api/EnterStoryDungeonMapNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<EnterStoryDungeonMapNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            var save = OpenLethe.Server.AccountFields.Get<StorySaveInfo>(account.StorySaveInfo) ?? new StorySaveInfo();
            save.currentinfo.cn = new Currentnode { f = p.floornumber, s = p.sectornumber, nid = p.nodeid };
            if (p.floornumber != save.currentinfo.scpn.f)
                save.currentinfo.scpn = new Currentnode { f = p.floornumber, s = p.sectornumber, nid = p.nodeid };
            save.currentinfo.pnids.Add(p.nodeid);
            save.currentinfo.nr = 1;
            account.StorySaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var result = new EnterStoryDungeonMapNodeResult { node = save.currentinfo.cn, nr = 3 };
            return Results.Json(global::ResponsePacket<EnterStoryDungeonMapNodeResult>.Ok(result, enterNodeId), global::PacketJson.Options);
        });

        var exitId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitStoryDungeon>();
        app.MapPost("/api/ExitStoryDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<ExitStoryDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var updated = OpenLethe.Server.ChapterProgress.RegisterWonNode(account, p.nodeid);
            await HandlerContext.SaveAsync(ctx);

            var response = global::ResponsePacket<ExitStoryDungeonResult>.Ok(new ExitStoryDungeonResult(), exitId);
            response.updated = updated;
            return Results.Json(response, global::PacketJson.Options);
        });

        var exitNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitStoryDungeonMapNode>();
        app.MapPost("/api/ExitStoryDungeonMapNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<ExitStoryDungeonMapNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            var save = OpenLethe.Server.AccountFields.Get<StorySaveInfo>(account.StorySaveInfo) ?? new StorySaveInfo();
            var ci = save.currentinfo;
            ci.egs.AddRange(p.updatedEgoGifts);
            ci.dul = p.dungeonunitlist;
            ci.ess = p.egoSkillStockList;

            var rewards = OpenLethe.Server.StoryMapData.GetAbRewardsByNodeId(save.dungeonid, ci.cn.nid.ToString());
            if (rewards is not null)
                foreach (var r in rewards)
                    if (!ci.egs.Exists(e => e.id == r.rewardId))
                        ci.egs.Add(new AcquiredEgogifts { id = r.rewardId });

            // Remove torch + power-up egs after one turn in the chapter-3 dungeon.
            if (save.dungeonid == 10301)
                ci.egs.RemoveAll(e => e.id == 991008 || e.id == 1032);

            // Unlock hidden next node.
            ci.opn = new();
            var next = (ci.cn.nid + 1).ToString();
            var nextNode = OpenLethe.Server.StoryMapData.GetStoryNodeById(save.dungeonid, next);
            if (nextNode is not null && nextNode.isHidden.ToLowerInvariant() == "true")
            {
                if (!long.TryParse(nextNode.id, out var hiddenId)) return Results.StatusCode(500);
                ci.opn = new() { hiddenId };
            }

            ci.pce.Clear();
            account.StorySaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var result = new ExitStoryDungeonMapNodeResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<ExitStoryDungeonMapNodeResult>.Ok(result, exitNodeId), global::PacketJson.Options);
        });

        var savePointId = global::PacketRouting.ResolvePacketId<global::ResPacket_ReturnSavePointStoryDungeonMap>();
        app.MapPost("/api/ReturnSavePointStoryDungeonMap", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();

            var save = OpenLethe.Server.AccountFields.Get<StorySaveInfo>(account.StorySaveInfo) ?? new StorySaveInfo();
            var ci = save.currentinfo;
            var map = OpenLethe.Server.StoryMapData.GetStoryMapById(save.dungeonid);

            while (true)
            {
                if (ci.pnids.Count == 0) return Results.StatusCode(500);
                var prevId = ci.pnids[^1];
                ci.pnids.RemoveAt(ci.pnids.Count - 1);

                var found = false;
                if (map is not null)
                    foreach (var floor in map.floors)
                    {
                        foreach (var sector in floor.sectors)
                            foreach (var node in sector.nodes)
                                if (node.encounter == "save" && node.id == prevId.ToString())
                                {
                                    ci.cn = new Currentnode { f = floor.count, s = sector.sectorNumber, nid = prevId };
                                    ci.pnids.Add(prevId);
                                    found = true;
                                    break;
                                }
                        if (found) break;
                    }

                if (found)
                {
                    account.StorySaveInfo = OpenLethe.Server.AccountFields.Set(save);
                    await HandlerContext.SaveAsync(ctx);
                    var result = new ReturnSavePointStoryDungeonMapResult { currentInfo = ci };
                    return Results.Json(global::ResponsePacket<ReturnSavePointStoryDungeonMapResult>.Ok(result, savePointId), global::PacketJson.Options);
                }
            }
        });

        return app;
    }
}
