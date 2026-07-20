var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => "ok");

// Temporary: one route to prove the helper works. Task 5 replaces this with
// the full generated set.
app.MapPacket<ReqPacket_EnterBossRaid, ResPacket_EnterBossRaid>("/api/EnterBossRaid");

app.Run();

// Exposed so WebApplicationFactory<Program> can find the entry point.
public partial class Program { }
