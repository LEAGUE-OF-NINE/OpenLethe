using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class PacketRoutingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PacketRoutingTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public void ResolvePacketId_StripsResPacketPrefix()
    {
        Assert.Equal(1696, PacketRouting.ResolvePacketId<ResPacket_EnterBossRaid>());
    }

    [Fact]
    public void ResolvePacketId_ThrowsForWrongPrefix()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PacketRouting.ResolvePacketId<string>());
        Assert.Contains("ResPacket_", ex.Message);
    }

    private sealed class ResPacket_NoSuchPacket;

    [Fact]
    public void ResolvePacketId_DefaultsToZeroForUnknownPacketName()
    {
        // The game client ignores the envelope's packetId value entirely, so an
        // unresolved name (route/client packet name drift, or a genuinely new
        // constant not yet extracted) must never be a fatal boot error - it
        // defaults to 0 and the endpoint still serves traffic normally.
        Assert.Equal(0, PacketRouting.ResolvePacketId<ResPacket_NoSuchPacket>());
    }

    [Fact]
    public async Task StaticEndpoint_ReturnsWellFormedEnvelope()
    {
        var client = _factory.CreateClient();

        var body = new
        {
            userAuth = new { uid = 1, dbid = 1, authCode = "test", version = "1", synchronousDataVersion = 0 },
            parameters = new { raidId = 3, difficulty = 1 },
        };

        var resp = await client.PostAsJsonAsync("/api/EnterBossRaid", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal("ok", root.GetProperty("state").GetString());
        Assert.Equal("product", root.GetProperty("serverInfo").GetProperty("version").GetString());
        Assert.Equal(1696, root.GetProperty("packetId").GetInt64());
        Assert.True(root.TryGetProperty("result", out _));

        // Optional envelope members must be absent, not null.
        Assert.False(root.TryGetProperty("updated", out _));
        Assert.False(root.TryGetProperty("synchronized", out _));
    }

    [Fact]
    public async Task StaticEndpoint_ToleratesEmptyParameters()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/EnterBossRaid", new
        {
            userAuth = new { uid = 0, dbid = 0, authCode = "", version = "", synchronousDataVersion = 0 },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task StaticEndpoint_ToleratesUnknownParameterFields()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/EnterBossRaid", new
        {
            userAuth = new { uid = 0, dbid = 0, authCode = "", version = "", synchronousDataVersion = 0 },
            parameters = new { raidId = 1, unknownFutureField = "x" },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task StaticEndpoint_EmptyBody_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsync(
            "/api/EnterBossRaid", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task StaticEndpoint_MalformedJson_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsync(
            "/api/EnterBossRaid",
            new StringContent("{not valid json", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
