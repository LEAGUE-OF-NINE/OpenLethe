using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class PacketRoutingTests : IClassFixture<PacketRoutingTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder b) =>
            b.UseSetting("Auth:JwtSecret", "packet-routing-test-secret-packet-routing-test-secret");
    }

    private readonly Factory _factory;
    private readonly string _token;

    public PacketRoutingTests(Factory factory)
    {
        _factory = factory;
        _token = factory.Services.GetRequiredService<OpenLethe.Server.Auth.JwtService>().Mint("test");
    }

    [Fact]
    public void ResolvePacketId_ReturnsTheConstant()
    {
        Assert.Equal(67, PacketRouting.ResolvePacketId<ResPacket_EnterBossRaid>());
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
    public void ResolvePacketId_IsNameIndependent()
    {
        // The game client ignores the envelope's packetId value entirely, so every
        // response carries the same one. A packet name the server has never seen
        // resolves exactly like a known one - there is nothing left to look up.
        Assert.Equal(67, PacketRouting.ResolvePacketId<ResPacket_NoSuchPacket>());
    }

    [Fact]
    public async Task StaticEndpoint_ReturnsWellFormedEnvelope()
    {
        var client = _factory.CreateClient();

        var body = new
        {
            userAuth = new { uid = 1, dbid = 1, authCode = _token, version = "1", synchronousDataVersion = 0 },
            parameters = new { raidId = 3, difficulty = 1 },
        };

        var resp = await client.PostAsJsonAsync("/api/AcquireAttendanceReward", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal("ok", root.GetProperty("state").GetString());
        Assert.Equal("product", root.GetProperty("serverInfo").GetProperty("version").GetString());
        Assert.Equal(
            PacketRouting.ResolvePacketId<ResPacket_AcquireAttendanceReward>(),
            root.GetProperty("packetId").GetInt64());
        Assert.True(root.TryGetProperty("result", out _));

        // Optional envelope members must be absent, not null.
        Assert.False(root.TryGetProperty("updated", out _));
        Assert.False(root.TryGetProperty("synchronized", out _));
    }

    [Fact]
    public async Task StaticEndpoint_ToleratesEmptyParameters()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/AcquireAttendanceReward", new
        {
            userAuth = new { uid = 0, dbid = 0, authCode = _token, version = "", synchronousDataVersion = 0 },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task StaticEndpoint_ToleratesUnknownParameterFields()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/AcquireAttendanceReward", new
        {
            userAuth = new { uid = 0, dbid = 0, authCode = _token, version = "", synchronousDataVersion = 0 },
            parameters = new { raidId = 1, unknownFutureField = "x" },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task StaticEndpoint_EmptyBody_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsync(
            "/api/AcquireAttendanceReward", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task StaticEndpoint_MalformedJson_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsync(
            "/api/AcquireAttendanceReward",
            new StringContent("{not valid json", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
