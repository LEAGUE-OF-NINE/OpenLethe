using System.Text.Json;
using System.Text.Json.Serialization;

/// The one and only serializer configuration for Limbus wire traffic.
/// Every one of these settings is load-bearing - see EnvelopeSerializationTests.
public static class PacketJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        // Client-extracted packet types use public FIELDS. Without this every
        // response serializes as "{}" - verified, not theoretical.
        IncludeFields = true,

        // No DefaultIgnoreCondition here, deliberately. The Rust reference
        // (lethe-server/models/src/packets.rs) has ZERO skip_serializing_if
        // attributes - every Option field in every result type serializes as
        // `null`. Only the envelope's `updated`/`synchronized`
        // (lethe-server/models/src/server.rs) skip when absent, and that's
        // opted into per-field with [JsonIgnore(Condition = WhenWritingNull)]
        // on those two members in Envelope.cs. Do NOT re-add a global
        // DefaultIgnoreCondition here - it silently drops nulls from every
        // response's `result` and breaks wire-compat diffing against Rust.

        // No PropertyNamingPolicy: packet field names are already camelCase and
        // must reach the client byte-identical.
    };
}
