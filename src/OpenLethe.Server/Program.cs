using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;
using OpenLethe.Server.Auth;

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

app.MapGet("/health", () => "ok");
app.MapAuth();
app.MapStaticPackets();

// EnterBossRaid is genuinely stateful (its Rust handler touches UserRepository,
// so it's correctly excluded from StaticRoutes.cs) but Task 4's regression
// tests exercise it as a stand-in stateless route. Kept registered here, as a
// stub, until cycle 3 gives it a real stateful handler.
// TODO(cycle-3): replace with a real stateful handler backed by UserRepository.
app.MapPacket<ReqPacket_EnterBossRaid, ResPacket_EnterBossRaid>("/api/EnterBossRaid");

app.Run();

// Exposed so WebApplicationFactory<Program> can find the entry point.
public partial class Program { }
