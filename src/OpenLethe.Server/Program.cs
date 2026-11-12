using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;
using OpenLethe.Server.Auth;
using OpenLethe.Server.Handlers;
using OpenLethe.Server.Locale;
using OpenLethe.Server.Login;

// Load a .env (searching up from the CWD) into process environment BEFORE the builder
// reads configuration, so ConnectionStrings__Postgres / Auth__JwtSecret can live in a
// local .env. Real environment variables already set take precedence; absent file is a no-op.
// Skipped under the test runner (which sets this): .env values become process
// environment variables, which outrank anything a WebApplicationFactory passes via
// UseSetting - so a developer's ConnectionStrings__Postgres would make every
// DB-free test migrate against their own database at startup.
if (Environment.GetEnvironmentVariable("OPENLETHE_NO_DOTENV") is null)
{
    DotNetEnv.Env.NoClobber().TraversePath().Load();
}

var builder = WebApplication.CreateBuilder(args);

// Rust: RequestBodyLimitLayer::new(2 * 1024 * 1024). Kestrel's own default is 30MB,
// which is a lot of memory to hand an unauthenticated caller once this is public.
// Configurable so a large /custom/upload payload can be allowed without a code change.
var maxBodyBytes = builder.Configuration.GetValue("MAX_REQUEST_BODY_BYTES", 2 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxBodyBytes);

// Rust: TimeoutLayer::new(Duration::from_secs(15)). Seconds rather than a TimeSpan
// so it can be turned down in tests, which is the only way to prove it fires.
var requestTimeout = TimeSpan.FromSeconds(
    builder.Configuration.GetValue("REQUEST_TIMEOUT_SECONDS", 15.0));
builder.Services.AddRequestTimeouts(o => o.DefaultPolicy = new RequestTimeoutPolicy
{
    Timeout = requestTimeout,
    TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
});

var connString = builder.Configuration.GetConnectionString("Postgres");

// Npgsql's default pool ceiling is 100 connections. Every one is a Postgres backend
// holding its own memory, and on the old 512MB DB VM a login burst that opened dozens of
// them is what got Postgres OOM-killed. 60 is three times the busiest load seen in the
// logs (21 active) and fits the 1GB VM. Past the cap, requests queue for a connection
// instead. An explicit "Maximum Pool Size" in the connection string still wins.
if (!string.IsNullOrWhiteSpace(connString))
{
    var csb = new Npgsql.NpgsqlConnectionStringBuilder(connString);
    if (!connString.Contains("Pool Size", StringComparison.OrdinalIgnoreCase)
        && !connString.Contains("MaxPoolSize", StringComparison.OrdinalIgnoreCase))
    {
        csb.MaxPoolSize = builder.Configuration.GetValue("Postgres:MaxPoolSize", 60);
    }
    connString = csb.ConnectionString;
}
builder.Services.AddDbContextPool<AppDbContext>(o => o.UseNpgsql(connString));
builder.Services.AddScoped<AccountStore>();

// Behind a proxy RemoteIpAddress is the edge's, identical for every caller, so the
// limiter below would bucket the whole internet as one. Fly's edge isn't in a
// private range, hence clearing the default known-proxy allowlist.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// The /auth surface is unauthenticated (login, captcha, OAuth handoff), so it is
// the brute-force target. Per-IP fixed window; everything else passes unlimited.
// 600 is deliberately loose: LetheLauncher polls /auth/token/poll at 1/s for up to
// 120s per login, so a tight ceiling breaks the normal flow, not just abuse.
var authPermitsPerMinute = builder.Configuration.GetValue("Auth:RateLimitPerMinute", 600);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        ctx.Request.Path.StartsWithSegments("/auth")
            ? System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                "auth:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                })
            : System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("none"));
});

// HS256 secret from config; generated ephemeral default so localhost needs no setup.
var jwtSecret = builder.Configuration["Auth:JwtSecret"]
    ?? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
builder.Services.AddSingleton(new JwtService(jwtSecret, TimeSpan.FromHours(72)));

// Backing store for the OAuth session handoff, the avatar/modular-doc caches, the
// locale rate limiter and the translation job store - all short-lived and per-process,
// which is what Rust's ExpiringMap was too.
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

// Port of Rust's CorsLayer: the Crux frontend is a different origin, and it sends
// credentials, so the origin must be named exactly - "*" is illegal with
// AllowCredentials. Comma-separated to allow a second origin (e.g. a preview
// deploy) without a code change.
//
// Unlike Rust, an unset FRONTEND_URL is not fatal: a self-hosted server with no
// frontend simply has no cross-origin caller to allow.
var corsOrigins = (builder.Configuration["FRONTEND_URL"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(o => o.TrimEnd('/'))
    .ToArray();

if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(corsOrigins)
        .WithMethods("GET", "POST")
        .WithHeaders("Cookie", "Content-Type", "Referer")
        .AllowCredentials()));
}

builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestBody
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseBody;
    o.RequestBodyLogLimit = 256 * 1024;
    o.ResponseBodyLogLimit = 256 * 1024;
    o.MediaTypeOptions.AddText("application/json");
});

var app = builder.Build();

// Migrate on startup only when a database is actually configured, so tests that
// exercise DB-free routes can boot without Postgres. (Rust migrates unconditionally.)
//
// Retried while the database is unreachable: a deploy or restart that lands while
// Postgres is down used to abort the process here, and Fly gives up after ten quick
// crashes. A SQL error from the migration itself (PostgresException) is not
// transient and still fails immediately.
if (!string.IsNullOrWhiteSpace(connString))
{
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
            break;
        }
        catch (Exception e) when (attempt < 12 && e is not Npgsql.PostgresException
            && (e is Npgsql.NpgsqlException || e.InnerException is Npgsql.NpgsqlException
                || e is System.Net.Sockets.SocketException || e is TimeoutException))
        {
            var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 15));
            app.Logger.LogWarning(e, "Database unreachable on startup (attempt {Attempt}); retrying in {Delay}", attempt, delay);
            Thread.Sleep(delay);
        }
    }
}

app.UseForwardedHeaders();   // before the rate limiter: it needs the real client IP

// Must precede routing: collapses "//api//Foo" so it matches "/api/Foo".
app.UsePathSanitizer();

// WebApplication implicitly runs routing as the very first pipeline step unless
// UseRouting is called explicitly - without this, endpoint matching happens
// against the ORIGINAL (unsanitized) path before UsePathSanitizer ever runs,
// regardless of the source-order of app.Use() calls relative to Map* calls.
app.UseRouting();

// Before UseJwtAuth: a preflight OPTIONS carries no packet envelope, so the JWT
// middleware would reject it with a 400 and the browser would report a CORS
// failure instead of the real error.
if (corsOrigins.Length > 0) app.UseCors();

app.UseRateLimiter();

app.UseRequestTimeouts();

app.UseHttpLogging();   // before UseJwtAuth so it sees the raw, unconsumed body

app.UseJwtAuth();   // 401s protected routes lacking a valid token; exempts /login,/auth,/health

// A fresh MdCatalog slot per request, which HandlerContext.ResolveAsync fills with the account's
// uploaded MD content. Async so the slot is scoped to this request and never outlives it.
app.Use(async (ctx, next) =>
{
    OpenLethe.Server.MirrorDungeon.Data.MdCatalog.BeginRequest();
    await next(ctx);
});

app.MapGet("/health", () => "ok");

app.MapModFiles();   // /Lethe.dll etc - LetheLauncher fetches these before launch
app.MapAuth();
app.MapDiscordAuth();
app.MapLocale();
app.MapDashboard();
app.MapCustomUpload();
app.MapSignInAsSteam();
app.MapGetTermsOfUseStateAll(); // real handler: returns terms as accepted (excluded from StaticRoutes)
app.MapStaticPackets();
app.MapLoadUserDataAll();
app.MapFetchLatestSynchronousData();
app.MapBossRaid();
app.MapUpdateFormation();
app.MapUseCoupon();
app.MapExitStageBattle();
app.MapExitStory();
app.MapUpdateAnnouncerPreset();
app.MapGetDungeonSaveInfoAll();
app.MapRailway();
app.MapStoryDungeon();
app.MapMirrorDungeon();
app.MapMirrorDungeonShop();
app.MapMirrorDungeonMap();
app.MapMirrorDungeonEvents();
app.MapMirrorDungeonRewards();
app.MapStoryMirrorDungeon();
app.MapStoryMirrorDungeonShop();
app.MapChapter10Rpg();

app.Run();

// Exposed so WebApplicationFactory<Program> can find the entry point.
public partial class Program { }
