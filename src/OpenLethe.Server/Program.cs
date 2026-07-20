var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Must precede routing: collapses "//api//Foo" so it matches "/api/Foo".
app.UsePathSanitizer();

// WebApplication implicitly runs routing as the very first pipeline step unless
// UseRouting is called explicitly - without this, endpoint matching happens
// against the ORIGINAL (unsanitized) path before UsePathSanitizer ever runs,
// regardless of the source-order of app.Use() calls relative to Map* calls.
app.UseRouting();

app.MapGet("/health", () => "ok");
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
