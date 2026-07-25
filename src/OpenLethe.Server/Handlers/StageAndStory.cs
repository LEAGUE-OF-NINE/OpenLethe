using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Ports of exit_stage_battle.rs and exit_story.rs. Both mark a won node in the
/// account's chapter state (via ChapterProgress) and carry the UpdatedFormat.
public static class StageAndStoryEndpoints
{
    public static IEndpointRouteBuilder MapExitStageBattle(this IEndpointRouteBuilder app)
    {
        var packetId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitStageBattle>();
        app.MapPost("/api/ExitStageBattle", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_ExitStageBattle>(ctx);
            if (p is null) return Results.BadRequest();

            var updated = new UpdatedFormat();
            if (p.iswin)
            {
                updated = ChapterProgress.RegisterWonNode(account, p.nodeid);
                await HandlerContext.SaveAsync(ctx);
            }

            var result = new global::ResPacket_ExitStageBattle
            {
                stageid = p.stageid,
                iswin = p.iswin,
                cleartype = 2,
            };
            var response = global::ResponsePacket<global::ResPacket_ExitStageBattle>.Ok(result, packetId);
            response.updated = updated;
            return Results.Json(response, global::PacketJson.Options);
        });
        return app;
    }

    public static IEndpointRouteBuilder MapExitStory(this IEndpointRouteBuilder app)
    {
        var packetId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitStory>();
        app.MapPost("/api/ExitStory", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_ExitStory>(ctx);
            if (p is null) return Results.BadRequest();

            var updated = ChapterProgress.RegisterWonNode(account, p.nodeid);
            await HandlerContext.SaveAsync(ctx);

            var response = global::ResponsePacket<global::ResPacket_ExitStory>.Ok(new(), packetId);
            response.updated = updated;
            return Results.Json(response, global::PacketJson.Options);
        });
        return app;
    }
}
