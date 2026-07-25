using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server;
using OpenLethe.Server.Auth;
using Xunit;

[Collection("postgres")]
public class AnnouncerHandlerTests(PostgresFixture db)
{
    [SkippableFact]
    public async Task UpdateAnnouncerPreset_PersistsIds()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var name = $"ann_{Guid.NewGuid():N}";
        string jwt;
        using (var scope = f.Services.CreateScope())
        {
            await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).GetOrCreateByUsernameAsync(name);
            jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        }
        var client = f.CreateClient();

        var body = new { userAuth = new { authCode = jwt }, parameters = new { presetId = 1, presetAnnouncerIds = new[] { 7, 42 } } };
        var resp = await client.PostAsJsonAsync("/api/UpdateAnnouncerPreset", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope2 = f.Services.CreateScope();
        var acc = await new AccountStore(scope2.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var stored = AccountFields.Get<List<long>>(acc!.Announcers);
        Assert.NotNull(stored);
        Assert.Equal(new List<long> { 7, 42 }, stored!);
    }
}
