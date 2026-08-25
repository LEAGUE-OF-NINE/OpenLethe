using OpenLethe.Data;
using OpenLethe.Server.MirrorDungeon.Rules;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Refraction Railway. State is one RailwayRun per dungeonId in the
/// RailwaySaveInfo column (the capture in docs/flows(2) has saves for dungeons
/// 6, 1001 and 1002 alive on one account simultaneously). Every transform here
/// is derived from that capture; game rules live in RailwayRules.
public static class RailwayEndpoints
{
    private static Dictionary<long, RailwayRun> Load(Account account) =>
        OpenLethe.Server.AccountFields.Get<Dictionary<long, RailwayRun>>(account.RailwaySaveInfo) ?? new();

    private static void Store(Account account, Dictionary<long, RailwayRun> state) =>
        account.RailwaySaveInfo = OpenLethe.Server.AccountFields.Set(state);

    private static RailwayRun Run(Dictionary<long, RailwayRun> state, long dungeonId)
    {
        if (state.TryGetValue(dungeonId, out var run)) return run;
        run = new RailwayRun();
        run.save.id = dungeonId;
        run.save.prevclearnode = -1;
        run.save.lastenternodeid = -1;
        state[dungeonId] = run;
        return run;
    }

    private static IResult Json<T>(T result, long packetId) =>
        Results.Json(global::ResponsePacket<T>.Ok(result, packetId), global::PacketJson.Options);

    public static IEndpointRouteBuilder MapRailway(this IEndpointRouteBuilder app)
    {
        var enterId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterRailwayDungeon>();
        app.MapPost("/api/EnterRailwayDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Railway);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<EnterRailwayDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var state = Load(account);
            var run = Run(state, p.dungeonId);
            var save = run.save;
            save.personalities = p.personalities ?? new();
            save.prevclearnode = 0;
            save.currentnode = 0;
            save.payreward = 1;
            save.lastenternodeid = -1;
            save.currentclearrotation = 0;
            save.buffsets = new();
            save.buffsetsbyegogift = new();
            save.startdate = RailwayRules.NowIso();
            save.initseed = save.currentseed = Random.Shared.Next(RailwayRules.SeedBound);
            save.extrarewardstate = RailwayRules.UnlockedExtraRewards(run);

            // A fresh run keeps the dungeon's known node ids but clears what was
            // recorded on them (ExitRailwayDungeon already does this; re-entering
            // an abandoned run must not leave earlier nodes marked cleared).
            run.nodes = run.nodes.Select(n => new UpdateNodeDatas { nodeid = n.nodeid }).ToList();
            var startNode = RailwayRules.FindOrDefaultNode(run.nodes, 0);
            startNode.nodestate = 1;

            Store(account, state);
            await HandlerContext.SaveAsync(ctx);

            return Json(new EnterRailwayDungeonResult { saveInfo = save, startNodeData = startNode }, enterId);
        });

        var enterNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterRailwayDungeonNode>();
        app.MapPost("/api/EnterRailwayDungeonNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Railway);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<EnterRailwayDungeonNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            var state = Load(account);
            var run = Run(state, p.dungeonId);
            // The carried-over party comes from the last CLEARED node, not from
            // nodeid-1: node 6 is skippable in the captured run and entering node
            // 7 still resumes node 5's party.
            var prev = RailwayRules.FindOrDefaultNode(run.nodes, run.save.prevclearnode);
            run.save.lastenternodeid = p.nodeid;

            Store(account, state);
            await HandlerContext.SaveAsync(ctx);

            return Json(new EnterRailwayDungeonNodeResult
            {
                nodeid = p.nodeid,
                deletedNodeIds = new(),
                abnormalityLogs = EventEngine.BattleAfterChoice(p.abnormalityids ?? new()),
                prevStatusData = prev.status,
                prevEgoStockData = prev.egostocks,
                prevEnemyData = prev.enemy,
                prevClearNodeId = run.save.prevclearnode,
                currentNodeId = run.save.currentnode,
                buffsetsbyegogift = RailwayRules.BuffsBelowNode(run.save.buffsetsbyegogift, p.nodeid),
            }, enterNodeId);
        });

        var exitNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitRailwayDungeonNode>();
        app.MapPost("/api/ExitRailwayDungeonNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Railway);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<ExitRailwayDungeonNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            var state = Load(account);
            var run = Run(state, p.dungeonId);
            var save = run.save;

            var node = new UpdateNodeDatas
            {
                nodeid = p.nodeid,
                egostocks = p.egoSkillStockList ?? new(),
                status = p.unitStatusList ?? new(),
                clearturn = p.clearTurn,
                playturn = 0,
                statistics = p.statistics ?? new(),
                enemy = RailwayRules.NormalizeEnemy(p.enemy),
                battleStates = p.battleStates ?? new(),
                nodestate = p.iswin ? 1 : -1,
            };
            RailwayRules.UpsertNode(run.nodes, node);

            save.lastenternodeid = -1;
            if (p.iswin)
            {
                save.currentnode = p.nodeid;
                save.prevclearnode = p.nodeid;
                save.lastclearnode = Math.Max(save.lastclearnode, p.nodeid);
                save.currentseed = Random.Shared.Next(RailwayRules.SeedBound);
                // Only ego-gift buff sets that actually carry buffs are recorded:
                // Refraction Railway 2 posts an empty {nid, buffs: []} on every node
                // and the real server's save keeps buffsetsbyegogift at [].
                if (p.buffsetbyegogift is { buffs.Count: > 0 })
                    RailwayRules.UpsertBuff(save.buffsetsbyegogift, p.buffsetbyegogift);
                save.extrarewardstate = RailwayRules.UnlockedExtraRewards(run);
            }

            Store(account, state);
            await HandlerContext.SaveAsync(ctx);

            return Json(new ExitRailwayDungeonNodeResult
            {
                abnormalityLogs = AbnormalityLogs(p.abnormalityLogs),
                saveInfo = save,
                updateNodeDatas = new List<UpdateNodeDatas> { node },
            }, exitNodeId);
        });

        var buffId = global::PacketRouting.ResolvePacketId<global::ResPacket_SelectRailwayDungeonBuff>();
        app.MapPost("/api/SelectRailwayDungeonBuff", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Railway);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<SelectRailwayDungeonBuffParams>(ctx);
            if (p is null) return Results.BadRequest();

            var state = Load(account);
            var run = Run(state, p.dungeonId);
            RailwayRules.ApplyBuffSelection(run, p.selectedBuffs ?? new());
            var node = RailwayRules.FindOrDefaultNode(run.nodes, run.save.prevclearnode);

            Store(account, state);
            await HandlerContext.SaveAsync(ctx);

            return Json(new SelectRailwayDungeonBuffResult { saveInfo = run.save, nodeData = node }, buffId);
        });

        var giveUpId = global::PacketRouting.ResolvePacketId<global::ResPacket_GiveUpRailwayDungeonNodeInBattle>();
        app.MapPost("/api/GiveUpRailwayDungeonNodeInBattle", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Railway);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<GiveUpRailwayDungeonNodeInBattleParams>(ctx);
            if (p is null) return Results.BadRequest();

            // Abandoning a battle leaves the node record untouched (the capture
            // returns the previously stored node verbatim, cleared flag and all);
            // only the "in this node" marker is dropped.
            var state = Load(account);
            var run = Run(state, p.dungeonid);
            var node = RailwayRules.FindOrDefaultNode(run.nodes, p.nodeid);
            run.save.lastenternodeid = -1;

            Store(account, state);
            await HandlerContext.SaveAsync(ctx);

            // ponytail: the real server enriches these logs from the account's
            // abnormality-encyclopedia store, which the port does not have
            // (GetAbnormalityLogData is still a static empty route). Echo instead.
            return Json(new GiveUpRailwayDungeonNodeInBattleResult
            {
                nodeData = node,
                abnormalityLogs = AbnormalityLogs(p.abnormalityLogs),
            }, giveUpId);
        });

        var restNodeId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitRailwayDungeonRestNode>();
        app.MapPost("/api/ExitRailwayDungeonRestNode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Railway);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<ExitRailwayDungeonRestNodeParams>(ctx);
            if (p is null) return Results.BadRequest();

            var state = Load(account);
            var run = Run(state, p.dungeonId);
            // Read the carried-over party BEFORE prevclearnode moves to this node.
            var prev = RailwayRules.FindOrDefaultNode(run.nodes, run.save.prevclearnode);
            var rested = RailwayRules.RestNodeStatus(p.dungeonId, p.personalities ?? new(), prev.status);
            var prevEgoStocks = prev.egostocks;
            var current = RailwayRules.FindOrDefaultNode(run.nodes, p.nodeid);

            run.save.currentnode = p.nodeid;
            run.save.prevclearnode = p.nodeid;
            run.save.lastclearnode = Math.Max(run.save.lastclearnode, p.nodeid);
            run.save.lastenternodeid = -1;
            current.nodeid = p.nodeid;
            current.nodestate = 1;
            current.status = rested;
            current.egostocks = prevEgoStocks;

            Store(account, state);
            await HandlerContext.SaveAsync(ctx);

            return Json(new ExitRailwayDungeonRestNodeResult
            {
                saveInfo = run.save, deletedNodeIds = new(), nodeData = current,
            }, restNodeId);
        });

        var exitId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitRailwayDungeon>();
        app.MapPost("/api/ExitRailwayDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Railway);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<ExitRailwayDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var state = Load(account);
            var run = Run(state, p.dungeonId);
            var save = run.save;

            var log = RailwayRules.BuildLog(run, run.logs.Count + 1, RailwayRules.NowIso());
            if (p.isClear)
            {
                run.logs.Add(log);
                save.clearnumber++;
                save.firstcleardate ??= log.date;
                save.lastclearrotation = Math.Max(save.lastclearrotation, save.currentclearrotation);
            }

            save.prevclearnode = -1;
            save.currentnode = 0;
            save.lastenternodeid = -1;
            save.personalities = new();
            save.startdate = null;
            save.currentclearrotation = 0;
            save.buffsets = new();
            save.buffsetsbyegogift = new();
            save.initseed = save.currentseed = 0;
            save.extrarewardstate = RailwayRules.UnlockedExtraRewards(run);
            run.nodes = run.nodes.Select(n => new UpdateNodeDatas { nodeid = n.nodeid }).ToList();

            Store(account, state);
            await HandlerContext.SaveAsync(ctx);

            // ponytail: `rewards` are the dungeon's bannerRewards (profile banners).
            // Nothing else in the port grants or owns banners, so the list stays
            // empty; wire it to RailwayData when a banner inventory exists.
            return Json(new ExitRailwayDungeonResult
            {
                isclear = p.isClear, saveInfo = save, currentLog = log, rewards = new(),
            }, exitId);
        });

        var acquireId = global::PacketRouting.ResolvePacketId<global::ResPacket_AcquireRailwayDungeonReward>();
        app.MapPost("/api/AcquireRailwayDungeonReward", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Railway);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<AcquireRailwayDungeonRewardParams>(ctx);
            if (p is null) return Results.BadRequest();

            var state = Load(account);
            var run = Run(state, p.dungeonId);
            var granted = RailwayRules.AcquireExtraRewards(run);

            Store(account, state);
            await HandlerContext.SaveAsync(ctx);

            return Json(new AcquireRailwayDungeonRewardResult { saveInfo = run.save, rewardList = granted }, acquireId);
        });

        var getAllId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetRailwayDungeonNodeAndLogAll>();
        app.MapPost("/api/GetRailwayDungeonNodeAndLogAll", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Railway);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<GetRailwayDungeonNodeAndLogAllParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = Run(Load(account), p.dungeonId);
            return Json(new GetRailwayDungeonNodeAndLogAllResult
            {
                railwaySaveInfo = run.save, nodeDatas = run.nodes, logDatas = run.logs,
            }, getAllId);
        });

        var saveInfoId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetRailwayDungeonSaveInfo>();
        app.MapPost("/api/GetRailwayDungeonSaveInfo", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Railway);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<GetRailwayDungeonNodeAndLogAllParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = Run(Load(account), p.dungeonId);
            return Json(new GetRailwayDungeonSaveInfoResult { railwaySaveInfo = run.save }, saveInfoId);
        });

        var nodeDatasId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetRailwayDungeonNodeDatas>();
        app.MapPost("/api/GetRailwayDungeonNodeDatas", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Railway);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<GetRailwayDungeonNodeAndLogAllParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = Run(Load(account), p.dungeonId);
            return Json(new GetRailwayDungeonNodeDatasResult { nodeDatas = run.nodes }, nodeDatasId);
        });

        var logsId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetRailwayDungeonLogs>();
        app.MapPost("/api/GetRailwayDungeonLogs", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Railway);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<GetRailwayDungeonNodeAndLogAllParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = Run(Load(account), p.dungeonId);
            return Json(new GetRailwayDungeonLogsResult { logDatas = run.logs }, logsId);
        });

        var extraRewardId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetRailwayDungeonExtraRewardStates>();
        app.MapPost("/api/GetRailwayDungeonExtraRewardStates", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Railway);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<GetRailwayDungeonExtraRewardStatesParams>(ctx);
            if (p is null) return Results.BadRequest();

            // A client body of {"dungeonIds": null} nulls the list - coalesce here,
            // after deserialization, since a field initializer would be overwritten.
            var state = Load(account);
            var result = new GetRailwayDungeonExtraRewardStatesResult
            {
                list = (p.dungeonIds ?? new()).Order().Select(id => new ExtraRewardStateByDungeonId
                {
                    dungeonId = id,
                    extraRewardState = state.TryGetValue(id, out var run) ? run.save.extrarewardstate : new(),
                }).ToList(),
            };
            return Json(result, extraRewardId);
        });

        return app;
    }


    /// Battle abnormality logs. The client posts one entry per abnormality it met,
    /// carrying the ids but no detail; the real server answers with those same
    /// entries - same abnormality ids, same `ps` body-part ids in the same order -
    /// sorted ascending by abnormality id, with only the per-battle rolls filled
    /// in. Verified against every logged battle in the capture.
    private static List<AbnormalityLogEntry> AbnormalityLogs(List<System.Text.Json.Nodes.JsonNode>? posted) =>
        (posted ?? new())
            .OfType<System.Text.Json.Nodes.JsonObject>()
            .OrderBy(l => (long?)l["id"] ?? 0)
            .Select(l => new AbnormalityLogEntry
            {
                id = (long?)l["id"] ?? 0,
                // k/s/p and the atrr/atkr permutations are per-battle RNG rolls the
                // port does not simulate (masked in the replay, as in MD).
                ps = (l["ps"] as System.Text.Json.Nodes.JsonArray)?
                        .OfType<System.Text.Json.Nodes.JsonObject>()
                        .Select(part => new AbnormalityLogPart { id = (long?)part["id"] ?? 0 })
                        .ToList() ?? new(),
            }).ToList();

}
