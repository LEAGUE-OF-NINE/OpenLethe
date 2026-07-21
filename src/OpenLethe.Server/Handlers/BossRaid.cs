using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace OpenLethe.Server.Handlers;

/// Boss-raid stateful endpoints. Save state lives in Account.BossRaidSaveInfo as a
/// client BossRaidSaveDataFormat. Ports of server/src/api/bossraid/*.rs.
public static class BossRaidEndpoints
{
    public static IEndpointRouteBuilder MapBossRaid(this IEndpointRouteBuilder app)
    {
        // GET save info: return the stored save state (null when absent), empty party/log.
        var getSaveInfoId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetBossRaidSaveInfo>();
        app.MapPost("/api/GetBossRaidSaveInfo", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();

            var save = OpenLethe.Server.AccountFields.Get<global::BossRaidSaveDataFormat>(account.BossRaidSaveInfo);
            var result = new global::ResPacket_GetBossRaidSaveInfo
            {
                saveInfo = save,
                partyDatas = new(),
                logData = new(),
            };
            return Results.Json(global::ResponsePacket<global::ResPacket_GetBossRaidSaveInfo>.Ok(result, getSaveInfoId), global::PacketJson.Options);
        });

        // GET states: one fixed raid entry (10001), state from the stored save (else 0).
        var getStatesId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetBossRaidStates>();
        app.MapPost("/api/GetBossRaidStates", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();

            var save = OpenLethe.Server.AccountFields.Get<global::BossRaidSaveDataFormat>(account.BossRaidSaveInfo);
            var result = new global::ResPacket_GetBossRaidStates
            {
                raidStates = new() { new global::BossRaidStateFormat { raidId = 10001, difficulty = 0, state = save?.state ?? 0 } },
            };
            return Results.Json(global::ResponsePacket<global::ResPacket_GetBossRaidStates>.Ok(result, getStatesId), global::PacketJson.Options);
        });

        // ENTER: create a fresh save state (state=2), persist, return it.
        var enterId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterBossRaid>();
        app.MapPost("/api/EnterBossRaid", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_EnterBossRaid>(ctx);
            if (p is null) return Results.BadRequest();

            var save = new global::BossRaidSaveDataFormat
            {
                raidId = p.raidId,
                difficulty = p.difficulty,
                state = 2,
                currentIdx = 0,
                isClear = false,
                personalities = new(),
                egostocks = new(),
                enemy = new(),
                startdate = "2026-05-02T22:55:55.055Z",
            };
            account.BossRaidSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var result = new global::ResPacket_EnterBossRaid { saveInfo = save };
            return Results.Json(global::ResponsePacket<global::ResPacket_EnterBossRaid>.Ok(result, enterId), global::PacketJson.Options);
        });

        // END: on reset, clear the save; return a zeroed save + empty log/rewards.
        var endId = global::PacketRouting.ResolvePacketId<global::ResPacket_EndBossRaid>();
        app.MapPost("/api/EndBossRaid", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_EndBossRaid>(ctx);
            if (p is null) return Results.BadRequest();

            if (p.reset)
            {
                account.BossRaidSaveInfo = "{}"; // Rust deletes the save on reset
                await HandlerContext.SaveAsync(ctx);
            }

            var save = new global::BossRaidSaveDataFormat
            {
                raidId = p.raidId, difficulty = 0, state = 0, currentIdx = 0, isClear = false,
                personalities = new(), egostocks = new(), enemy = new(), startdate = "",
            };
            var result = new global::ResPacket_EndBossRaid { saveInfo = save, logData = new(), rewards = new() };
            return Results.Json(global::ResponsePacket<global::ResPacket_EndBossRaid>.Ok(result, endId), global::PacketJson.Options);
        });

        // START BATTLE: stash the chosen personalities' ids on the save; return enemy + egostocks.
        var startId = global::PacketRouting.ResolvePacketId<global::ResPacket_StartBossRaidBattle>();
        app.MapPost("/api/StartBossRaidBattle", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_StartBossRaidBattle>(ctx);
            if (p is null) return Results.BadRequest();

            var save = OpenLethe.Server.AccountFields.Get<global::BossRaidSaveDataFormat>(account.BossRaidSaveInfo);
            if (save is null) return Results.BadRequest(); // Rust errors when no active raid

            save.personalities = (p.personalities ?? new()).Select(x => x.pid).ToList();
            account.BossRaidSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var result = new global::ResPacket_StartBossRaidBattle { enemy = save.enemy, egostocks = save.egostocks };
            return Results.Json(global::ResponsePacket<global::ResPacket_StartBossRaidBattle>.Ok(result, startId), global::PacketJson.Options);
        });

        // EXIT (client route: ExitBossRaidBattle). Win clears the save; loss advances it.
        var exitId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitBossRaidBattle>();
        app.MapPost("/api/ExitBossRaidBattle", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_ExitBossRaidBattle>(ctx);
            if (p is null) return Results.BadRequest();

            var save = OpenLethe.Server.AccountFields.Get<global::BossRaidSaveDataFormat>(account.BossRaidSaveInfo);
            if (save is null) return Results.BadRequest();

            global::ResPacket_ExitBossRaidBattle result;
            if (p.isWin)
            {
                account.BossRaidSaveInfo = "{}"; // clear on win
                await HandlerContext.SaveAsync(ctx);
                result = new global::ResPacket_ExitBossRaidBattle { saveInfo = null };
            }
            else
            {
                save.enemy = p.enemy ?? new();
                save.currentIdx += 1;
                account.BossRaidSaveInfo = OpenLethe.Server.AccountFields.Set(save);
                await HandlerContext.SaveAsync(ctx);
                result = new global::ResPacket_ExitBossRaidBattle { saveInfo = save };
            }
            return Results.Json(global::ResponsePacket<global::ResPacket_ExitBossRaidBattle>.Ok(result, exitId), global::PacketJson.Options);
        });

        return app;
    }
}
