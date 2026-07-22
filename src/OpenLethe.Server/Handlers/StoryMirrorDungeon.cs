using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Ports of server/src/api/storymirrordungeon/{enter_story_mirror_dungeon,
/// update_story_mirror_dungeon, acquire_start_ego_gifts_story_mirror_dungeon,
/// enter_story_mirror_dungeon_map_node, update_story_mirror_dungeon_map_node,
/// exit_story_mirror_dungeon_map_node}.rs.
public static class StoryMirrorDungeonEndpoints
{
    public static IEndpointRouteBuilder MapStoryMirrorDungeon(this IEndpointRouteBuilder app)
    {
        var enterId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterStoryMirrorDungeon>();
        var updateId = global::PacketRouting.ResolvePacketId<global::ResPacket_UpdateStoryMirrorDungeon>();
        var acquireStartId = global::PacketRouting.ResolvePacketId<global::ResPacket_AcquireStartEgoGiftsStoryMirrorDungeon>();
        var enterMapNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterStoryMirrorDungeonMapNode>();
        var updateMapNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_UpdateStoryMirrorDungeonMapNode>();
        var exitMapNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitStoryMirrorDungeonMapNode>();

        app.MapPost("/api/EnterStoryMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<EnterStoryMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            // Builds a FRESH save and ignores any existing one - matching Rust exactly.
            var save = new StoryMirrorSaveInfo();
            save.currentinfo.eid = -1;

            save.currentinfo.seps = new List<StartEgoGiftPoolSets>
            {
                new() { setId = 0, keyword = "Combustion", pool = new() { 9001, 9009, 9103 } },
                new() { setId = 1, keyword = "Laceration", pool = new() { 9005, 9029, 9108 } },
                new() { setId = 2, keyword = "Vibration", pool = new() { 9044, 9086, 9113 } },
                new() { setId = 3, keyword = "Burst", pool = new() { 9047, 9093, 9117 } },
                new() { setId = 4, keyword = "Sinking", pool = new() { 9041, 9054, 9124 } },
                new() { setId = 5, keyword = "Breath", pool = new() { 9046, 9051, 9129 } },
                new() { setId = 6, keyword = "Charge", pool = new() { 9043, 9052, 9134 } },
            };
            save.currentinfo.sepsCreated = 1;

            save.currentinfo.ess = new List<EgoSkillStock>
            {
                new() { t = "CR", n = 0 },
                new() { t = "SC", n = 0 },
                new() { t = "AM", n = 0 },
                new() { t = "SH", n = 0 },
                new() { t = "AZ", n = 0 },
                new() { t = "IN", n = 0 },
                new() { t = "VI", n = 0 },
            };
            save.currentinfo.cost = 200;

            save.dungeonid = p.dungeonid;

            account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new EnterStoryMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<EnterStoryMirrorDungeonResult>.Ok(result, enterId), global::PacketJson.Options);
        });

        app.MapPost("/api/UpdateStoryMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<StoryMirrorSaveInfo>(account.StoryMdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<UpdateStoryMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var formation = p.formation.Select(unit => new Dungeonunitlist2
            {
                sp = 50,
                upidx = new List<long>(),
                mlos = 0,
                pid = unit.nextPersonalityId,
                ch = 10000,
                cm = 0,
                mhos = 0,
                g = 0,
                l = 60, // DEFAULT_PERSONALITY_LEVEL
                es = unit.egos.Select(ego => new Egos { id = ego.nextEgoId, g = 0, idx = 0 }).ToList(),
                isp = 0,
            }).ToList();

            save.currentinfo.seps.Clear();
            save.currentinfo.dul = formation;

            account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new UpdateStoryMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<UpdateStoryMirrorDungeonResult>.Ok(result, updateId), global::PacketJson.Options);
        });

        app.MapPost("/api/AcquireStartEgoGiftsStoryMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<StoryMirrorSaveInfo>(account.StoryMdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<AcquireStartEgoGiftsStoryMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            try
            {
                StoryMdMapGen.GenerateNewFloor(0, save.dungeonid, save);
            }
            catch (KeyNotFoundException)
            {
                return Results.StatusCode(500);
            }

            save.currentinfo.egs.AddRange(p.selectedEgoGiftIds.Select(id => new AcquiredEgogifts { id = id }));

            account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new AcquireStartEgoGiftsStoryMirrorDungeonResult
            {
                egoGifts = save.currentinfo.egs,
                startEgoGiftPoolSets = new(),
                startEgoGiftCreatedCount = 2,
            };
            return Results.Json(global::ResponsePacket<AcquireStartEgoGiftsStoryMirrorDungeonResult>.Ok(result, acquireStartId), global::PacketJson.Options);
        });

        app.MapPost("/api/EnterStoryMirrorDungeonMapNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<StoryMirrorSaveInfo>(account.StoryMdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<EnterStoryMirrorDungeonMapNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            save.currentinfo.shop.egpool = MdEgoData.GetRandomMdEgoGifts(4);

            account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new EnterStoryMirrorDungeonMapNodeResult
            {
                abnormalityLogs = new(),
                passingNodeIds = new(),
                // Echoes the REQUEST's currentnode - it is NOT written to the save.
                currentNode = p.currentnode,
                shopInfo = save.currentinfo.shop,
                egogifts = save.currentinfo.egs,
                prevdul = new(),
                preves = new(),
            };
            return Results.Json(global::ResponsePacket<EnterStoryMirrorDungeonMapNodeResult>.Ok(result, enterMapNodeId), global::PacketJson.Options);
        });

        app.MapPost("/api/UpdateStoryMirrorDungeonMapNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<StoryMirrorSaveInfo>(account.StoryMdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<UpdateStoryMirrorDungeonMapNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            // Rust: pce.first().and_then(|e| e.nei), else the save's map.ns node whose nid
            // matches the request's currentnode.nid -> its eid. Unlike the story-DUNGEON
            // sibling, when NEITHER resolves this returns 500 rather than falling back to -1.
            long? eid = save.currentinfo.pce.FirstOrDefault()?.nei;
            eid ??= save.map.ns.FirstOrDefault(n => n.nid == p.currentnode.nid)?.eid;
            if (eid is not { } matchingEid) return Results.StatusCode(500);

            // Empty choiceEventData.sl defaults the index to 0 (Rust unwrap_or(&0)) rather
            // than erroring - unlike the story-DUNGEON sibling.
            var choiceIdx = p.choiceEventData.sl.Count > 0 ? p.choiceEventData.sl[0] : 0;
            var cs = p.choiceEventData.cs;

            // choiceIdx is client-controlled; a naked (int) cast wraps on overflow, so
            // narrow it via the clamping helper instead (see ClampChoiceIndex).
            var next = MdEventManager.ProcessEvent(matchingEid, MdEventManager.ClampChoiceIndex(choiceIdx), cs, new StoryMdEventSave(save));

            // cs = -1 and ri = 0 are literals in Rust, NOT the request's values.
            save.currentinfo.pce.Insert(0, new ChoiceEventData
            {
                sl = new List<long> { choiceIdx }, cs = -1, ri = 0, nei = next,
            });

            account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new UpdateStoryMirrorDungeonMapNodeResult
            {
                prevChoiceEvent = new(),
                currentEgoGifts = save.currentinfo.egs,
                dungeonUnitList = save.currentinfo.dul,
            };
            return Results.Json(global::ResponsePacket<UpdateStoryMirrorDungeonMapNodeResult>.Ok(result, updateMapNodeId), global::PacketJson.Options);
        });

        app.MapPost("/api/ExitStoryMirrorDungeonMapNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<StoryMirrorSaveInfo>(account.StoryMdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<ExitStoryMirrorDungeonMapNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            save.currentinfo.cn = p.currentnode;
            save.currentinfo.dul = p.dungeonunitlist;

            // Some nodes don't send egoSkillStockList, such as rest stops.
            if (p.isupdatedEgoSkillStock == 1)
                save.currentinfo.ess = p.egoSkillStockList;

            save.currentinfo.nr = 1;
            save.currentinfo.pce.Clear();

            // Clear blocked purchased ego gifts from the shop.
            save.currentinfo.shop.peg.Clear();

            var matchingNode = save.map.ns.FirstOrDefault(n => n.nid == p.currentnode.nid);
            // Rust returns 400 here (not 500), unlike the plain-MD sibling.
            if (matchingNode is null) return Results.BadRequest();

            save.currentinfo.eid = matchingNode.eid;

            if (matchingNode.e == 5 || matchingNode.e == 2)
            {
                var egoGiftPool = StoryMdThemeData.GetTheme(save.dungeonid).GetCombinedEgoGiftPool();
                // ponytail: Rust does rng.gen_range(0..ego_gift_pool.len()), which panics on
                // an empty pool. The derived theme pools (Task 2) already make an empty pool
                // unreachable for every known dungeon id, so just skip instead of throwing.
                if (egoGiftPool.Count > 0)
                {
                    var randomElement = egoGiftPool[Random.Shared.Next(egoGiftPool.Count)];
                    save.currentinfo.rre = new List<RemainRewardEvent>
                    {
                        new() { rt = "GetEgogift", se = 1, sh = 1, pool = new List<long> { randomElement } },
                    };
                }
            }

            save.currentinfo.cost += MdCost.GetDefaultCost(matchingNode.e, 0);

            account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new ExitStoryMirrorDungeonMapNodeResult { currentInfo = save.currentinfo, abnormalityLogs = new() };
            return Results.Json(global::ResponsePacket<ExitStoryMirrorDungeonMapNodeResult>.Ok(result, exitMapNodeId), global::PacketJson.Options);
        });

        return app;
    }
}
