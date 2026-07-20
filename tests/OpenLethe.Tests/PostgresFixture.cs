using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;
using Testcontainers.PostgreSql;
using Xunit;

// Shared Postgres for all DB-backed tests. Prefers a Testcontainers container;
// falls back to DATABASE_TEST_URL if set; if neither is available the collection
// is effectively unusable and its tests should be skipped by the guard below.
[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture> { }

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    public string ConnectionString { get; private set; } = "";
    public bool Available { get; private set; }

    public async Task InitializeAsync()
    {
        var envUrl = Environment.GetEnvironmentVariable("DATABASE_TEST_URL");
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            ConnectionString = envUrl;
            Available = true;
        }
        else
        {
            try
            {
                _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
                await _container.StartAsync();
                ConnectionString = _container.GetConnectionString();
                Available = true;
            }
            catch
            {
                Available = false; // Docker not present - tests using RequireDb() will skip.
                return;
            }
        }

        await using var ctx = NewContext();
        await ctx.Database.MigrateAsync();
    }

    public AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    // Call at the top of every DB test (which must be [SkippableFact]); no-op if a
    // DB is available, skips the test otherwise. Skip.IfNot comes from Xunit.SkippableFact.
    public void RequireDb()
    {
        Skip.IfNot(Available, "No Postgres available (Docker or DATABASE_TEST_URL required).");
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }
}
