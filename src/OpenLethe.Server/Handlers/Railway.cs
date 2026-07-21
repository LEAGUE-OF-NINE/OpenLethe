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

        var exitNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitRailwayDungeonNode>();
        app.MapPost("/api/ExitRailwayDungeonNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<ExitRailwayDungeonNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            var save = OpenLethe.Server.AccountFields.Get<RailwaySaveInfo>(account.RailwaySaveInfo) ?? new RailwaySaveInfo();
            var nodeData = OpenLethe.Server.AccountFields.Get<List<UpdateNodeDatas>>(account.RailwayNodeData) ?? new();

            var newNode = new UpdateNodeDatas
            {
                nodeid = p.nodeid, egostocks = p.egoSkillStockList, status = p.unitStatusList,
                clearturn = p.clearTurn, playturn = 0, statistics = p.statistics, enemy = p.enemy, nodestate = 1,
            };
            if (p.iswin)
            {
                save.currentnode = p.nodeid;
                save.prevclearnode = p.nodeid;
                var buffs = OpenLethe.Server.AccountFields.Get<List<Buffsetsbyegogift>>(account.RailwayBuffs) ?? new();
                RailwayHelpers.UpsertBuff(buffs, p.buffsetbyegogift);
                account.RailwayBuffs = OpenLethe.Server.AccountFields.Set(buffs);
                account.RailwaySaveInfo = OpenLethe.Server.AccountFields.Set(save);
            }
            else
            {
                newNode.nodestate = -1;
            }
            RailwayHelpers.UpsertNode(nodeData, newNode);
            account.RailwayNodeData = OpenLethe.Server.AccountFields.Set(nodeData);
            await HandlerContext.SaveAsync(ctx);

            var result = new ExitRailwayDungeonNodeResult
            {
                saveInfo = save, abnormalityLogs = new(), nodeData = newNode,
                updateNodeDatas = new List<UpdateNodeDatas> { newNode },
            };
            return Results.Json(global::ResponsePacket<ExitRailwayDungeonNodeResult>.Ok(result, exitNodeId), global::PacketJson.Options);
        });

        var exitId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitRailwayDungeon>();
        app.MapPost("/api/ExitRailwayDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<ExitRailwayDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var save = OpenLethe.Server.AccountFields.Get<RailwaySaveInfo>(account.RailwaySaveInfo) ?? new RailwaySaveInfo();
            account.RailwayBuffs = OpenLethe.Server.AccountFields.Set(new List<Buffsetsbyegogift>()); // clear buffs
            await HandlerContext.SaveAsync(ctx);

            var log = new CurrentLog
            {
                idx = -1,
                personalities = save.personalities,
                turnspernode = Enumerable.Range(0, 8).Select(nid => new Turnspernode { nid = nid, turn = 0 }).ToList(),
                detailstatistics = Enumerable.Range(0, 5).Select(id => new Detailstatistics { collectionId = id, personalities = new(), statistics = new() }).ToList(),
                date = "2024-07-04T14:19:11.000Z",
            };
            var result = new ExitRailwayDungeonResult
            {
                isclear = p.isClear,
                saveInfo = new RailwaySaveInfo { id = 5, prevclearnode = -1, lastenternodeid = -1 },
                currentLog = log,
                rewards = new(),
            };
            return Results.Json(global::ResponsePacket<ExitRailwayDungeonResult>.Ok(result, exitId), global::PacketJson.Options);
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
