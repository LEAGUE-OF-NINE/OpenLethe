using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;

/// Registration for stateless Limbus endpoints.
///
/// In Rust each of these needed its own handler file because `static_response` is
/// generic over the response type. In C# one generic method covers all 108, so a
/// stateless endpoint costs one line instead of a file.
public static class PacketRouting
{
    private const string ResPrefix = "ResPacket_";

    /// Derives the packet ID from the response type name.
    /// ResPacket_EnterBossRaid -> PacketIds.For("EnterBossRaid") -> 1696.
    public static long ResolvePacketId<TRes>()
    {
        var name = typeof(TRes).Name;

        if (!name.StartsWith(ResPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected a type named {ResPrefix}*, got '{name}'.");
        }

        return PacketIds.For(name[ResPrefix.Length..]);
    }

    /// Registers a stateless POST endpoint that echoes back a default response.
    public static IEndpointRouteBuilder MapPacket<TReq, TRes>(
        this IEndpointRouteBuilder app,
        string route)
        where TRes : new()
    {
        // Resolved once at startup, not per request: a missing packet ID becomes a
        // boot failure rather than a runtime 500 on a rarely-hit endpoint.
        var packetId = ResolvePacketId<TRes>();

        app.MapPost(route, async (HttpContext ctx) =>
        {
            // Body is read and discarded. Stateless endpoints ignore their input,
            // but we must still drain it so the client sees a clean request cycle.
            try
            {
                _ = await System.Text.Json.JsonSerializer
                    .DeserializeAsync<RequestPacket<TReq>>(ctx.Request.Body, PacketJson.Options);
            }
            catch (System.Text.Json.JsonException)
            {
                // Mirrors axum's Json<T> extractor: an unparseable body (empty,
                // absent, or malformed) is rejected with 400 before any handler
                // logic runs, never a 500 with a leaked stack trace.
                return Results.BadRequest();
            }

            return Results.Json(
                ResponsePacket<TRes>.Ok(new TRes(), packetId),
                PacketJson.Options);
        });

        return app;
    }
}
