using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

public static class UseCouponEndpoints
{
    public static IEndpointRouteBuilder MapUseCoupon(this IEndpointRouteBuilder app)
    {
        var packetId = global::PacketRouting.ResolvePacketId<global::ResPacket_UseCoupon>();
        app.MapPost("/api/UseCoupon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();

            var userInfo = AccountFields.Get<UserInfo>(account.UserInfo) ?? AccountDefaults.DefaultUserInfo();
            var updated = new UpdatedFormat
            {
                userInfo = userInfo,
                personalityList = AccountDefaults.DerivePersonalities(account.Personalities),
            };
            var result = new global::ResPacket_UseCoupon { state = 1, rewards = new() };
            var response = global::ResponsePacket<global::ResPacket_UseCoupon>.Ok(result, packetId);
            response.updated = updated;
            return Results.Json(response, global::PacketJson.Options);
        });
        return app;
    }
}
