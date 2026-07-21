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

        return app;
    }
}
