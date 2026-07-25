using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using OpenLethe.Data;
using OpenLethe.Server.Auth;

namespace OpenLethe.Server.Login;

public static class SignInAsSteamEndpoint
{
    public static IEndpointRouteBuilder MapSignInAsSteam(this IEndpointRouteBuilder app)
    {
        var packetId = global::PacketRouting.ResolvePacketId<global::ResPacket_SignInAsSteam>();

        app.MapPost("/login/SignInAsSteam", async (HttpContext ctx, AccountStore store, JwtService jwt, IConfiguration cfg, CancellationToken ct) =>
        {
            var req = await System.Text.Json.JsonSerializer.DeserializeAsync<
                global::RequestPacket<global::ReqPacket_SignInAsSteam>>(ctx.Request.Body, global::PacketJson.Options, ct);

            var hex = req?.parameters?.steamToken;
            if (string.IsNullOrEmpty(hex)) return Results.BadRequest();

            byte[] raw;
            try { raw = Convert.FromHexString(hex); }
            catch { return Results.BadRequest(); }

            var token = Encoding.UTF8.GetString(raw);

            // Dev mode (Auth:DevAcceptAnyToken): accept ANY jwt as an identity -
            // read its subject without verifying the signature, auto-create that
            // account, and hand back a token WE signed so later /api/ calls (which
            // the middleware verifies for real) keep working. Off by default.
            string sub;
            OpenLethe.Data.Account? account;
            string authCode;
            if (cfg.GetValue("Auth:DevAcceptAnyToken", false))
            {
                if (!JwtService.TryReadSubjectUnverified(token, out sub)) return Results.BadRequest();
                account = await store.GetOrCreateByUsernameAsync(sub, ct);
                authCode = jwt.Mint(sub);
            }
            else
            {
                if (!jwt.TryVerify(token, out sub)) return Results.BadRequest();
                account = await store.FindByUsernameAsync(sub, ct);
                if (account is null) return Results.BadRequest();
                authCode = token;
            }

            var result = new global::ResPacket_SignInAsSteam
            {
                userAuth = new global::UserAuthFormat
                {
                    uid = account.IngameId,
                    public_id = account.IngameId,
                    db_id = 0,
                    auth_code = authCode,
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
