using System.Text.Json;

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
        // Every response this route will ever send is the same bytes: `new TRes()` is a
        // constant and so is the ambient envelope around it. Serialize once at startup
        // instead of rebuilding and re-serializing the graph on each of the 100+ static
        // routes' requests. (ResolvePacketId throws on a non-response type, so a bad
        // registration fails at boot rather than per request.)
        var body = JsonSerializer.SerializeToUtf8Bytes(
            ResponsePacket<TRes>.Ok(new TRes(), ResolvePacketId<TRes>()), PacketJson.Options);

        app.MapPost(route, async (HttpContext ctx) =>
        {
            // Stateless endpoints discard their input, but a body that is unparseable
            // OR whose `parameters` don't fit TReq is still a 400, never a 500 with a
            // leaked stack trace - that is what axum's Json<T> extractor does, and it
            // type-checks the target. JwtAuthMiddleware validated only the ENVELOPE, so
            // its stash saves the re-tokenization here, not this type check. Routes it
            // exempts (/login/*) leave no stash and still read the stream.
            try
            {
                if (ctx.Items[OpenLethe.Server.Handlers.HandlerContext.ParamsItemKey] is JsonElement p)
                {
                    // Undefined = no `parameters` key at all, always legal on these routes.
                    if (p.ValueKind != JsonValueKind.Undefined) _ = p.Deserialize<TReq>(PacketJson.Options);
                }
                else
                {
                    _ = await JsonSerializer.DeserializeAsync<RequestPacket<TReq>>(
                        ctx.Request.Body, PacketJson.Options);
                }
            }
            catch (JsonException)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            // Written straight out rather than through Results.Json, which streams with
            // no known length and so frames the response chunked. Setting ContentLength
            // sends it unchunked instead: same body bytes, and the framing now matches
            // axum's Json (serialize to a buffer, emit Content-Length).
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength = body.Length;
            await ctx.Response.Body.WriteAsync(body);
        });

        return app;
    }
}
