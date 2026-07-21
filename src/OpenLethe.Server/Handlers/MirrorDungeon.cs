using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Ports of server/src/api/md/*.rs. Save state lives in Account.MdSaveInfo as a server
/// Wire MirrorOriginSaveInfo. This cycle: the enter/re-enter foundation.
public static class MirrorDungeonEndpoints
{
    public static IEndpointRouteBuilder MapMirrorDungeon(this IEndpointRouteBuilder app)
    {
        var enterId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterMirrorDungeon>();
        app.MapPost("/api/EnterMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<EnterMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var save = BuildFreshSave(p.dungeonid, p.idx);
            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var result = new EnterMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<EnterMirrorDungeonResult>.Ok(result, enterId), global::PacketJson.Options);
        });

        var reEnterId = global::PacketRouting.ResolvePacketId<global::ResPacket_ReEnterMirrorDungeon>();
        app.MapPost("/api/ReEnterMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo) ?? new MirrorOriginSaveInfo();
            var result = new ReEnterMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<ReEnterMirrorDungeonResult>.Ok(result, reEnterId), global::PacketJson.Options);
        });

        return app;
    }

    // Port of enter_mirror_dungeon.rs: a fresh MD save with the fixed start-gift pools + stocks.
    private static MirrorOriginSaveInfo BuildFreshSave(long dungeonId, long idx)
    {
        var seps = new List<StartEgoGiftPoolSets>
        {
            new() { setId = 0, keyword = "Combustion", pool = new() { 9001, 9009, 9103 } },
            new() { setId = 1, keyword = "Laceration", pool = new() { 9005, 9029, 9108 } },
            new() { setId = 2, keyword = "Vibration", pool = new() { 9044, 9086, 9113 } },
            new() { setId = 3, keyword = "Burst", pool = new() { 9047, 9093, 9117 } },
            new() { setId = 4, keyword = "Sinking", pool = new() { 9041, 9054, 9124 } },
            new() { setId = 5, keyword = "Breath", pool = new() { 9046, 9051, 9129 } },
            new() { setId = 6, keyword = "Charge", pool = new() { 9043, 9052, 9134 } },
            new() { setId = 7, keyword = "Slash", pool = new() { 9032, 9194, 9140 } },
            new() { setId = 8, keyword = "Penetrate", pool = new() { 9030, 9198, 9145 } },
            new() { setId = 9, keyword = "Hit", pool = new() { 9012, 9202, 9150 } },
        };
        var stocks = new List<EgoSkillStock>
        {
            new() { t = "CR", n = 0 }, new() { t = "SC", n = 0 }, new() { t = "AM", n = 0 },
            new() { t = "SH", n = 0 }, new() { t = "AZ", n = 0 }, new() { t = "IN", n = 0 },
            new() { t = "VI", n = 0 },
        };
        return new MirrorOriginSaveInfo
        {
            dungeonId = dungeonId,
            idx = idx,
            currentInfo = new CurrentInfo
            {
                eid = -1,
                sepsId = 0,
                seps = seps,
                sepsCreated = 1,
                ri = 1,
                cost = 500,
                usedcost = 0,
                shop = new ShopInfo(),
                startKeyword = "None",
                startBufPoint = 0,
                cfs = new List<Cfs> { new() { floor = -1, difficulty = 0 } },
                efs = new Efs { rpf = 0 },
                cn = new Currentnode { f = 0, s = 0, nid = 0 },
                nr = 0,
                ess = stocks,
                dn = 0,
            },
            dungeonMap = new DungeonMap(),
            choiceEventList = new(),
            addUserExp = 0,
            statistics = new(),
            encounterstatistics = new List<long> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            isEndDungeon = 0,
            isReset = 0,
            version = 2,
        };
    }
}
