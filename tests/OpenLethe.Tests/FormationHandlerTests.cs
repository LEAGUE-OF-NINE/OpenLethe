using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server;
using OpenLethe.Server.Auth;
using Xunit;

[Collection("postgres")]
public class FormationHandlerTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new
    {
        userAuth = new { authCode = jwt },
        parameters = p,
    };

    [SkippableFact]
    public async Task UpdateFormation_PersistsMergedById()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var name = $"form_{Guid.NewGuid():N}";
        string jwt;
        using (var scope = f.Services.CreateScope())
        {
            await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).GetOrCreateByUsernameAsync(name);
            jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        }
        var client = f.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/UpdateFormation", Body(jwt, new { formation = new { id = 5, formationDetails = new object[0], formationNameFormat = new object[0] } }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // Persisted: the stored Formations column now contains formation id 5.
        using var scope2 = f.Services.CreateScope();
        var acc = await new AccountStore(scope2.ServiceProvider.GetRequiredService<AppDbContext>()).FindByUsernameAsync(name);
        var stored = AccountFields.Get<List<global::FormationFormat>>(acc!.Formations);
        Assert.NotNull(stored);
        Assert.Contains(stored!, x => x.id == 5);
    }
}
