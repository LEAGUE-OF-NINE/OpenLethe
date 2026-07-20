using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Data;
using OpenLethe.Server.Auth;

namespace OpenLethe.Server.Login;

public static class SignInAsSteamEndpoint
{
    public static IEndpointRouteBuilder MapSignInAsSteam(this IEndpointRouteBuilder app)
    {
        var packetId = global::PacketRouting.ResolvePacketId<global::ResPacket_SignInAsSteam>();

        app.MapPost("/login/SignInAsSteam", async (HttpContext ctx, AccountStore store, JwtService jwt, CancellationToken ct) =>
        {
            var req = await System.Text.Json.JsonSerializer.DeserializeAsync<
                global::RequestPacket<global::ReqPacket_SignInAsSteam>>(ctx.Request.Body, global::PacketJson.Options, ct);

            var hex = req?.parameters?.steamToken;
            if (string.IsNullOrEmpty(hex)) return Results.BadRequest();

            byte[] raw;
            try { raw = Convert.FromHexString(hex); }
            catch { return Results.BadRequest(); }

            var token = Encoding.UTF8.GetString(raw);
            if (!jwt.TryVerify(token, out var sub)) return Results.BadRequest();

            var account = await store.FindByUsernameAsync(sub, ct);
            if (account is null) return Results.BadRequest();

            var result = new global::ResPacket_SignInAsSteam
            {
                userAuth = new global::UserAuthFormat
                {
                    uid = account.IngameId,
                    public_id = account.IngameId,
                    db_id = 0,
                    auth_code = token,
                    last_login_date = "2025-03-31T15:10:00.000Z",
                    last_update_date = "2025-03-31T15:10:00.000Z",
                    data_version = 16,
                },
                accountInfo = new global::AccountInfoFormat { uid = (ulong)account.IngameId },
                walletCurrency = "USD",
            };

            return Results.Json(
                global::ResponsePacket<global::ResPacket_SignInAsSteam>.Ok(result, packetId),
                global::PacketJson.Options);
        });

        return app;
    }
}
