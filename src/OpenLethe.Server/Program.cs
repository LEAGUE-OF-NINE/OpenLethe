using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;
using OpenLethe.Server.Auth;
using OpenLethe.Server.Handlers;
using OpenLethe.Server.Login;

// Load a .env (searching up from the CWD) into process environment BEFORE the builder
// reads configuration, so ConnectionStrings__Postgres / Auth__JwtSecret can live in a
// local .env. Real environment variables already set take precedence; absent file is a no-op.
DotNetEnv.Env.NoClobber().TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

var connString = builder.Configuration.GetConnectionString("Postgres");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connString));
builder.Services.AddScoped<AccountStore>();

// HS256 secret from config; generated ephemeral default so localhost needs no setup.
var jwtSecret = builder.Configuration["Auth:JwtSecret"]
    ?? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
builder.Services.AddSingleton(new JwtService(jwtSecret, TimeSpan.FromHours(72)));

var app = builder.Build();

// Migrate on startup only when a database is actually configured, so tests that
// exercise DB-free routes can boot without Postgres. (Rust migrates unconditionally.)
if (!string.IsNullOrWhiteSpace(connString))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

// Must precede routing: collapses "//api//Foo" so it matches "/api/Foo".
app.UsePathSanitizer();

// WebApplication implicitly runs routing as the very first pipeline step unless
// UseRouting is called explicitly - without this, endpoint matching happens
// against the ORIGINAL (unsanitized) path before UsePathSanitizer ever runs,
// regardless of the source-order of app.Use() calls relative to Map* calls.
app.UseRouting();

app.UseJwtAuth();   // 401s protected routes lacking a valid token; exempts /login,/auth,/health

app.MapGet("/health", () => "ok");
app.MapAuth();
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

app.Run();

// Exposed so WebApplicationFactory<Program> can find the entry point.
public partial class Program { }
