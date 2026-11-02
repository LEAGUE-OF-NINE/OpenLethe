using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server.Auth;

// The per-account MdCatalog cache, keyed on Account.CustomMdRevision: an MD request reads the
// stamp, never the upload documents, and a changed upload is never served stale.
[Collection("postgres")]
public class MdCatalogCacheTests(PostgresFixture db)
{
    private static object Body(string jwt, object p) => new
    {
        userAuth = new { authCode = jwt },
        parameters = p,
    };

    private static object GiftUpload(long id) => new
    {
        list = new object[] { new { list = new object[] { new { id, price = 150L, tag = new[] { "TIER_1" }, keyword = "Burst" } } } },
    };

    private static async Task<(string Jwt, string Name)> NewAccountAsync(DbWebAppFactory f)
    {
        var name = $"mdcache_{Guid.NewGuid():N}";
        using var scope = f.Services.CreateScope();
        await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>()).GetOrCreateByUsernameAsync(name);
        return (scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name), name);
    }

    private static async Task<long> RevisionAsync(DbWebAppFactory f, string name)
    {
        using var scope = f.Services.CreateScope();
        return (await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>())
            .FindByUsernameAsync(name))!.CustomMdRevision;
    }

    private static async Task<List<long>> RecordGiftsAsync(HttpClient client, string jwt)
    {
        var resp = await client.PostAsJsonAsync("/api/GetMirrorDungeonEgoGiftRecord", Body(jwt, new { }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("result").GetProperty("acquiredegogifts")
            .EnumerateArray().Select(e => e.GetInt64()).ToList();
    }

    [SkippableFact]
    public async Task Revision_IsZeroUntilAnUpload_AndOnlyMovesWhenTheContentChanges()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, name) = await NewAccountAsync(f);
        var client = f.CreateClient();

        Assert.Equal(0, await RevisionAsync(f, name));

        await client.PostAsJsonAsync("/custom/upload/ego-gift-mirrordungeon", Body(jwt, GiftUpload(990101)));
        var first = await RevisionAsync(f, name);
        Assert.NotEqual(0, first);

        // The client re-sends every upload at each login: the same content keeps the stamp.
        await client.PostAsJsonAsync("/custom/upload/ego-gift-mirrordungeon", Body(jwt, GiftUpload(990101)));
        Assert.Equal(first, await RevisionAsync(f, name));

        // An upload of another kind that changes nothing (empty -> empty) keeps it too.
        await client.PostAsJsonAsync("/custom/upload/mirrordungeon-egogift-droppool", Body(jwt, new { list = Array.Empty<object>() }));
        Assert.Equal(first, await RevisionAsync(f, name));

        await client.PostAsJsonAsync("/custom/upload/ego-gift-mirrordungeon", Body(jwt, GiftUpload(990102)));
        Assert.True(await RevisionAsync(f, name) > first);
    }

    [SkippableFact]
    public async Task ChangedUpload_IsNotServedFromTheCache()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccountAsync(f);
        var client = f.CreateClient();

        await client.PostAsJsonAsync("/custom/upload/ego-gift-mirrordungeon", Body(jwt, GiftUpload(990101)));
        Assert.Contains(990101L, await RecordGiftsAsync(client, jwt));
        Assert.Contains(990101L, await RecordGiftsAsync(client, jwt)); // second request: cached

        await client.PostAsJsonAsync("/custom/upload/ego-gift-mirrordungeon", Body(jwt, GiftUpload(990102)));
        var after = await RecordGiftsAsync(client, jwt);
        Assert.Contains(990102L, after);
        Assert.DoesNotContain(990101L, after);
    }

    [SkippableFact]
    public async Task AccountWithoutUploads_GetsTheStaticTablesOnly()
    {
        db.RequireDb();
        await using var f = new DbWebAppFactory(db.ConnectionString);
        var (jwt, _) = await NewAccountAsync(f);
        var client = f.CreateClient();

        var gifts = await RecordGiftsAsync(client, jwt);
        Assert.Contains(9001L, gifts);
        Assert.DoesNotContain(990101L, gifts);
    }
}
