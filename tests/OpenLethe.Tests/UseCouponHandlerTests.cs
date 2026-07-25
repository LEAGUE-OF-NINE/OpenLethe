using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server.Auth;
using Xunit;

[Collection("postgres")]
public class UseCouponHandlerTests(PostgresFixture db)
{
    [SkippableFact]
    public async Task UseCoupon_ReturnsState1_WithPersonalitiesAndUserInfo()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var name = $"coup_{Guid.NewGuid():N}";
        string jwt;
        using (var scope = f.Services.CreateScope())
        {
            await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).GetOrCreateByUsernameAsync(name);
            jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        }
        var client = f.CreateClient();

        var body = new { userAuth = new { authCode = jwt }, parameters = new { code = "FREESTUFF" } };
        var resp = await client.PostAsJsonAsync("/api/UseCoupon", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetProperty("result").GetProperty("state").GetInt32());
        Assert.Equal(1234, doc.RootElement.GetProperty("updated").GetProperty("userInfo").GetProperty("uid").GetInt32());
        Assert.True(doc.RootElement.GetProperty("updated").GetProperty("personalityList").GetArrayLength() > 0);
    }
}
