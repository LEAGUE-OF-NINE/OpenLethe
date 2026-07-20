using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;

namespace OpenLethe.Server.Auth;

public static class AuthEndpoints
{
    public sealed class LoginRequest { public string username { get; set; } = ""; }
    public sealed class LoginResponse { public string token { get; set; } = ""; }

    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        // Simplified localhost login: username-only, trust on first use.
        app.MapPost("/auth/login", async (LoginRequest req, AccountStore store, JwtService jwt, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.username))
                return Results.BadRequest();

            await store.GetOrCreateByUsernameAsync(req.username, ct);
            return Results.Json(new LoginResponse { token = jwt.Mint(req.username) });
        });

        return app;
    }
}
