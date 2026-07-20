using System.Text.Json.Serialization;

// Ported from lethe-server/models/src/server.rs.
// Field names must match the wire format exactly; do not rename.

public class ServerInfo
{
    public string version = "product";
}

public class UserAuthMain
{
    public int uid;
    public int dbid;
    [JsonPropertyName("authCode")]
    public string auth_code = "";
    public string version = "";
    public int synchronousDataVersion;
}

public class RequestPacket<T>
{
    public UserAuthMain userAuth = new();
    public T parameters = default!;
}

public class ResponsePacket<T>
{
    public ServerInfo serverInfo = new();
    public string state = "ok";

    // Null members are omitted via WhenWritingNull. Cycles 2-4 populate these;
    // object? keeps cycle 1 free of the types they need.
    public object? updated;

    public T result = default!;
    public object? synchronized;
    public long packetId;

    public static ResponsePacket<T> Ok(T result, long packetId) => new()
    {
        result = result,
        packetId = packetId,
    };
}
