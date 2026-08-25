using OpenLethe.Data;

namespace OpenLethe.Server.Auth;

public static class AuthEndpoints
{
    public sealed class LoginRequest { public string username { get; set; } = ""; }
    public sealed class LoginResponse { public string token { get; set; } = ""; }

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        // Simplified localhost login: username-only, trust on first use. That is an
        // account-takeover hole on a public deployment (anyone can claim any
        // non-Discord name), so Auth:EnableLocalLogin=false hides the route and
        // leaves Discord OAuth as the only way in.
        app.MapPost("/auth/login", async (LoginRequest req, AccountStore store, JwtService jwt,
            IConfiguration cfg, CancellationToken ct) =>
        {
            if (!cfg.GetValue("Auth:EnableLocalLogin", true))
                return Results.NotFound();

            if (string.IsNullOrWhiteSpace(req.username))
                return Results.BadRequest();

            // Null means the name belongs to a Discord account - it is not claimable here.
            if (await store.GetOrCreateByUsernameAsync(req.username, ct) is null)
                return Results.Conflict("That username belongs to a Discord account; sign in through /auth/discord.");

            return Results.Json(new LoginResponse { token = jwt.Mint(req.username) });
        });

        return app;
    }
}
