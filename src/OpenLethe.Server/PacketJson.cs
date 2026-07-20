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

        // Rust omits `updated` and `synchronized` when absent (skip_serializing_if).
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // No PropertyNamingPolicy: packet field names are already camelCase and
        // must reach the client byte-identical.
    };
}
