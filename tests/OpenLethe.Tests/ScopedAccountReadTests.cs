using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenLethe.Data;
using OpenLethe.Server.Auth;
using Xunit;

// HandlerContext.ResolveAsync(ctx, SaveColumn) reads Id + one jsonb column instead of
// the whole fourteen-column row, which is what the MD/Railway packet storms pay for on
// every request. Two things have to hold for that to be safe:
//   - the SELECT really is narrow (otherwise the change bought nothing), and
//   - saving through a scoped account leaves the thirteen unloaded columns alone.
// The scope-to-handler pairings are pinned by the existing per-handler suites: get one
// wrong and SaveAsync's guard throws, which turns their 200 assertions red.
[Collection("postgres")]
public class ScopedAccountReadTests(PostgresFixture db)
{
    // Captures the SQL EF emits so the narrowing can be asserted, not assumed. Read off
    // EF's command log rather than through an IInterceptor: AddDbContextPool builds its
    // options before the factory's extra services land, so a DI-registered interceptor
    // never gets attached.
    private sealed class SqlCapturingFactory(string connString) : DbWebAppFactory(connString)
    {
        public readonly List<string> Sql = [];

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(s => s.AddSingleton<ILoggerProvider>(new Sink(Sql)));
        }

        // EventId equality covers the name too, so one logger over every category is
        // enough to pick EF's command log out. Kept off the factory itself: its inherited
        // Dispose would satisfy ILoggerProvider.Dispose and could tear the server down.
        private sealed class Sink(List<string> sink) : ILoggerProvider, ILogger
        {
            public ILogger CreateLogger(string category) => this;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Dispose() { }

            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? e,
                Func<TState, Exception?, string> formatter)
            {
                if (id == RelationalEventId.CommandExecuted) lock (sink) sink.Add(formatter(state, e));
            }
        }
    }

    private async Task<(SqlCapturingFactory Factory, HttpClient Client, string Name, string Jwt)> BootAsync(string prefix)
    {
        var f = new SqlCapturingFactory(db.ConnectionString);
        var name = $"{prefix}_{Guid.NewGuid():N}";
        using var scope = f.Services.CreateScope();
        await new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>())
            .GetOrCreateByUsernameAsync(name);
        var jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        lock (f.Sql) f.Sql.Clear();   // drop the setup's own queries; only the request matters
        return (f, f.CreateClient(), name, jwt);
    }

    private static object Body(string jwt, object parameters) =>
        new { userAuth = new { authCode = jwt }, parameters };

    [SkippableFact]
    public async Task EnterMirrorDungeon_SelectsOnlyIdAndMdSaveInfo()
    {
        db.RequireDb();
        var (f, client, _, jwt) = await BootAsync("scope_read");
        await using var _f = f;

        var resp = await client.PostAsJsonAsync("/api/EnterMirrorDungeon",
            Body(jwt, new { dungeonid = 7, idx = 0 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var select = Assert.Single(f.Sql, s => s.Contains("FROM accounts", StringComparison.Ordinal));
        Assert.Contains("\"MdSaveInfo\"", select);
        // The thirteen columns this handler never reads must not be in the projection -
        // they are jsonb documents Postgres would detoast and ship for nothing.
        Assert.DoesNotContain("\"StoryMdSaveInfo\"", select);
        Assert.DoesNotContain("\"RailwaySaveInfo\"", select);
        Assert.DoesNotContain("\"Personalities\"", select);
        Assert.DoesNotContain("\"ChapterState\"", select);
    }

    [SkippableFact]
    public async Task ScopedWrite_LeavesTheOtherColumnsUntouched()
    {
        db.RequireDb();
        var (f, client, name, jwt) = await BootAsync("scope_write");
        await using var _f = f;

        // Real data in columns the MD handler never loads. If the scoped save wrote the
        // whole entity back, the blanked-out unloaded properties would erase these.
        // Read back through JsonNode: jsonb reorders object keys, so the stored text is
        // not the text that went in.
        using (var scope = f.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var a = await ctx.Accounts.SingleAsync(x => x.Username == name);
            a.Personalities = """[{"personality_id":1,"level":60}]""";
            a.RailwaySaveInfo = """{"6":{"save":{"cleared":true}}}""";
            await ctx.SaveChangesAsync();
        }

        var resp = await client.PostAsJsonAsync("/api/EnterMirrorDungeon",
            Body(jwt, new { dungeonid = 7, idx = 0 }));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using (var scope = f.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Username is itself one of the blanked columns, so finding the row by name
            // at all proves the blanking never reached the database.
            var a = await ctx.Accounts.SingleAsync(x => x.Username == name);
            Assert.NotEqual("{}", a.MdSaveInfo);   // the scoped write landed

            // ...and nothing else moved.
            var personalities = JsonNode.Parse(a.Personalities)!.AsArray();
            Assert.Equal(1, (int)personalities[0]!["personality_id"]!);
            Assert.Equal(60, (int)personalities[0]!["level"]!);
            Assert.True((bool)JsonNode.Parse(a.RailwaySaveInfo)!["6"]!["save"]!["cleared"]!);
        }
    }
}
