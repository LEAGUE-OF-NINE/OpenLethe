using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace OpenLethe.Server.Handlers;

/// Port of change_current_announcer.rs at the client route /api/UpdateAnnouncerPreset
/// (name-drift). Overwrites the account's stored announcer id list; empty ack.
/// ponytail: the client also sends presetId (a preset-slot index) - the reference
/// server has no preset concept and stores a single announcer list, so presetId is
/// ignored. Add per-preset storage only if the client turns out to depend on it.
public static class AnnouncerEndpoints
{
    public static IEndpointRouteBuilder MapUpdateAnnouncerPreset(this IEndpointRouteBuilder app)
    {
        var packetId = global::PacketRouting.ResolvePacketId<global::ResPacket_UpdateAnnouncerPreset>();
        app.MapPost("/api/UpdateAnnouncerPreset", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_UpdateAnnouncerPreset>(ctx);
            if (p is null) return Results.BadRequest();

            var ids = (p.presetAnnouncerIds ?? new()).Select(x => (long)x).ToList();
            account.Announcers = OpenLethe.Server.AccountFields.Set(ids);
            await HandlerContext.SaveAsync(ctx);

            return Results.Json(global::ResponsePacket<global::ResPacket_UpdateAnnouncerPreset>.Ok(new(), packetId), global::PacketJson.Options);
        });
        return app;
    }
}
