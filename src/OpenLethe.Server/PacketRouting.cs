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

    /// The envelope must carry a packetId, but the game client never reads its
    /// value - any number serves. So every response gets the same one, and the
    /// extracted per-packet ID table (and its generator) are gone.
    public const long PacketId = 67;

    /// Returns the constant packet ID. The only work left here is asserting that
    /// callers name a real response packet type, which is what keeps handlers
    /// honest about which contract they answer.
    public static long ResolvePacketId<TRes>()
    {
        var name = typeof(TRes).Name;

        if (!name.StartsWith(ResPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected a type named {ResPrefix}*, got '{name}'.");
        }

        return PacketId;
    }

    /// Registers a stateless POST endpoint that echoes back a default response.
    public static IEndpointRouteBuilder MapPacket<TReq, TRes>(
        this IEndpointRouteBuilder app,
        string route)
        where TRes : new()
    {
        // Resolved once at startup, not per request. A miss defaults to 0 - the
        // client ignores packetId, so this can never block the server from booting.
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
