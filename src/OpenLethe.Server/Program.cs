var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => "ok");
app.MapStaticPackets();

// EnterBossRaid is genuinely stateful (its Rust handler touches UserRepository,
// so it's correctly excluded from StaticRoutes.cs) but Task 4's regression
// tests exercise it as a stand-in stateless route. Kept registered here, as a
// stub, until cycle 3 gives it a real stateful handler.
app.MapPacket<ReqPacket_EnterBossRaid, ResPacket_EnterBossRaid>("/api/EnterBossRaid");

app.Run();

// Exposed so WebApplicationFactory<Program> can find the entry point.
public partial class Program { }
