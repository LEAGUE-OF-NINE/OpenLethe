using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace OpenLethe.Server.Login;

/// /login/GetTermsOfUseStateAll returns the terms of use as ALREADY ACCEPTED
/// (version 1, state 1 = AGREE) so the client skips the user-agreement prompt.
/// Ported from lethe-server get_terms_of_use_state_all.rs. This is a
/// static_response with POPULATED data the generic MapPacket can't reproduce
/// (it would return an empty list), so it needs a real handler and is excluded
/// from the generated StaticRoutes.
public static class GetTermsOfUseStateEndpoint
{
    public static IEndpointRouteBuilder MapGetTermsOfUseStateAll(this IEndpointRouteBuilder app)
    {
        var packetId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetTermsOfUseStateAll>();

        app.MapPost("/login/GetTermsOfUseStateAll", () =>
        {
            var result = new global::ResPacket_GetTermsOfUseStateAll
            {
                termsOfUseStateList = new() { new global::TermsOfUseState { version = 1, state = 1 } },
            };
            return Results.Json(
                global::ResponsePacket<global::ResPacket_GetTermsOfUseStateAll>.Ok(result, packetId),
                global::PacketJson.Options);
        });

        return app;
    }
}
