using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Ports of server/src/api/railway/*.rs. Save state lives in three Account columns
/// (RailwaySaveInfo, RailwayNodeData, RailwayBuffs) as server Wire types.
public static class RailwayEndpoints
{
    public static IEndpointRouteBuilder MapRailway(this IEndpointRouteBuilder app)
    {
        var enterId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterRailwayDungeon>();
        app.MapPost("/api/EnterRailwayDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<EnterRailwayDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var prev = OpenLethe.Server.AccountFields.Get<RailwaySaveInfo>(account.RailwaySaveInfo) ?? new RailwaySaveInfo();
            var save = new RailwaySaveInfo
            {
                id = 5, prevclearnode = 0, currentnode = 0, lastclearnode = prev.lastclearnode,
                personalities = p.personalities, payreward = 1, rewardstate = 7,
                extrarewardstate = new(), firstcleardate = "", currentclearrotation = 0,
                lastenternodeid = -1, lastclearrotation = 0, buffsets = new(), buffsetsbyegogift = new(),
                initseed = 57515885, currentseed = 57515885,
            };
            var startNode = new UpdateNodeDatas
            {
                nodeid = 0, egostocks = new(), status = DeriveStatus(p.personalities),
                clearturn = 0, playturn = 0, statistics = new(), enemy = new PrevEnemyData(), nodestate = 1,
            };
            account.RailwayNodeData = OpenLethe.Server.AccountFields.Set(new List<UpdateNodeDatas> { startNode });
            account.RailwaySaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var result = new EnterRailwayDungeonResult { saveInfo = save, startNodeData = startNode };
            return Results.Json(global::ResponsePacket<EnterRailwayDungeonResult>.Ok(result, enterId), global::PacketJson.Options);
        });

        var getAllId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetRailwayDungeonNodeAndLogAll>();
        app.MapPost("/api/GetRailwayDungeonNodeAndLogAll", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();

            var nodes = OpenLethe.Server.AccountFields.Get<List<UpdateNodeDatas>>(account.RailwayNodeData) ?? new();
            var result = new GetRailwayDungeonNodeAndLogAllResult { nodeDatas = nodes, logDatas = new() };
            return Results.Json(global::ResponsePacket<GetRailwayDungeonNodeAndLogAllResult>.Ok(result, getAllId), global::PacketJson.Options);
        });

        var enterNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterRailwayDungeonNode>();
        app.MapPost("/api/EnterRailwayDungeonNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<EnterRailwayDungeonNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            var save = OpenLethe.Server.AccountFields.Get<RailwaySaveInfo>(account.RailwaySaveInfo) ?? new RailwaySaveInfo();
            var nodeData = OpenLethe.Server.AccountFields.Get<List<UpdateNodeDatas>>(account.RailwayNodeData) ?? new();
            var buffs = OpenLethe.Server.AccountFields.Get<List<Buffsetsbyegogift>>(account.RailwayBuffs) ?? new();

            var prev = RailwayHelpers.FindOrDefaultNode(nodeData, p.nodeid - 1); // not persisted (matches Rust)
            var result = new EnterRailwayDungeonNodeResult
            {
                nodeid = p.nodeid, deletedNodeIds = new(), abnormalityLogs = new(),
                prevStatusData = prev.status, prevEgoStockData = prev.egostocks, prevEnemyData = prev.enemy,
                prevClearNodeId = save.prevclearnode, currentNodeId = prev.nodeid,
                buffsetsbyegogift = RailwayHelpers.BuffsBelowNode(buffs, p.nodeid),
            };
            return Results.Json(global::ResponsePacket<EnterRailwayDungeonNodeResult>.Ok(result, enterNodeId), global::PacketJson.Options);
        });

        var restNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitRailwayDungeonRestNode>();
        app.MapPost("/api/ExitRailwayDungeonRestNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<ExitRailwayDungeonRestNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            var save = OpenLethe.Server.AccountFields.Get<RailwaySaveInfo>(account.RailwaySaveInfo) ?? new RailwaySaveInfo();
            var nodeData = OpenLethe.Server.AccountFields.Get<List<UpdateNodeDatas>>(account.RailwayNodeData) ?? new();

            var prevEgoStocks = RailwayHelpers.FindOrDefaultNode(nodeData, p.nodeid - 1).egostocks;
            var current = RailwayHelpers.FindOrDefaultNode(nodeData, p.nodeid);
            save.currentnode = p.nodeid;
            save.prevclearnode = p.nodeid;
            current.nodeid = p.nodeid;
            current.status = DeriveStatus(p.personalities);
            current.egostocks = prevEgoStocks;

            account.RailwayNodeData = OpenLethe.Server.AccountFields.Set(nodeData);
            account.RailwaySaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var result = new ExitRailwayDungeonRestNodeResult { saveInfo = save, deletedNodeIds = new(), nodeData = current };
            return Results.Json(global::ResponsePacket<ExitRailwayDungeonRestNodeResult>.Ok(result, restNodeId), global::PacketJson.Options);
        });

        return app;
    }

    private static List<PrevStatusData> DeriveStatus(List<Personalities> personalities) =>
        personalities.Select(p => new PrevStatusData
        {
            pid = p.pid, hp = 10000, mp = 0, isp = 0, sin = new Sin(),
            egos = p.es, sp = 0, lv = 60, g = p.g, gi = p.gi, pord = 1,
        }).ToList();
}
