using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server;

namespace OpenLethe.Server.Handlers;

public static class FormationEndpoints
{
    public static IEndpointRouteBuilder MapUpdateFormation(this IEndpointRouteBuilder app)
    {
        var packetId = global::PacketRouting.ResolvePacketId<global::ResPacket_UpdateFormation>();
        app.MapPost("/api/UpdateFormation", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_UpdateFormation>(ctx);
            if (p?.formation is null) return Results.BadRequest();

            var stored = AccountFields.Get<List<global::FormationFormat>>(account.Formations) ?? new();
            var merged = AccountFields.MergeById(stored, new List<global::FormationFormat> { p.formation }, ff => ff.id);
            account.Formations = AccountFields.Set(merged);
            await HandlerContext.SaveAsync(ctx);

            return Results.Json(global::ResponsePacket<global::ResPacket_UpdateFormation>.Ok(new(), packetId), global::PacketJson.Options);
        });
        return app;
    }
}
