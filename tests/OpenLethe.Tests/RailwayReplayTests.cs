using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server.Auth;
using OpenLethe.Tests.Replay;

/// Replays the Refraction Railway capture (docs/flows(2), dungeon 1002 - the
/// rerun of Refraction Railway 2) against the live server: before every request
/// the captured ground-truth railway state is written to the account row, then
/// our response is diffed against the captured one through the per-endpoint mask.
/// The tracker only ever advances from CAPTURED responses.
[Collection("postgres")]
public class RailwayReplayTests(PostgresFixture db)
{
    private static readonly HashSet<string> Covered = new()
    {
        "/api/EnterRailwayDungeon",
        "/api/EnterRailwayDungeonNode",
        "/api/ExitRailwayDungeonNode",
        "/api/SelectRailwayDungeonBuff",
        "/api/GiveUpRailwayDungeonNodeInBattle",
        "/api/ExitRailwayDungeon",
        "/api/AcquireRailwayDungeonReward",
        "/api/GetRailwayDungeonNodeAndLogAll",
        "/api/GetRailwayDungeonExtraRewardStates",
    };

    /// Every dungeon a record talks about. EnterRailwayDungeon is exempt: it
    /// STARTS a run, so it is correct without prior ground truth.
    private static IEnumerable<long> Dungeons(JsonNode req)
    {
        if (req["parameters"] is not JsonObject p) return Array.Empty<long>();
        if (p["dungeonIds"] is JsonArray many) return many.Select(x => (long)x!);
        var one = p["dungeonId"] ?? p["dungeonid"];
        return one is null ? Array.Empty<long>() : new[] { (long)one };
    }

    [SkippableFact]
    public async Task Replays_RailwayRun_Matches()
    {
        db.RequireDb();
        await using var factory = new DbWebAppFactory(db.ConnectionString);

        var name = $"rwreplay_{Guid.NewGuid():N}";
        string jwt;
        using (var scope = factory.Services.CreateScope())
        {
            var store = new AccountStore(scope.ServiceProvider.GetRequiredService<AppDbContext>());
            await store.GetOrCreateByUsernameAsync(name);
            jwt = scope.ServiceProvider.GetRequiredService<JwtService>().Mint(name);
        }
        var client = factory.CreateClient();
        var failures = new List<string>();

        foreach (var (runId, file) in FixtureLoader.RailwayRuns)
        {
            var truth = new RailwayTruthState();
            foreach (var rec in FixtureLoader.Records(file))
            {
                using (var scope = factory.Services.CreateScope())
                {
                    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var acc = ctx.Accounts.First(a => a.Username == name);
                    acc.RailwaySaveInfo = truth.Json;
                    await ctx.SaveChangesAsync();
                }

                if (Covered.Contains(rec.Path) && rec.Req is not null
                    && Dungeons(rec.Req).All(truth.Knows))
                {
                    var req = rec.Req.DeepClone();
                    if (req["userAuth"] is JsonObject ua) ua["authCode"] = jwt;
                    var resp = await client.PostAsync(rec.Path, JsonContent.Create(req));
                    var ours = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
                    var diffs = JsonDiff.Compare(ours, rec.Res, ReplayMasks.For(runId, rec.Path, rec.Seq));
                    if (diffs.Count > 0)
                        failures.Add($"[{runId}] seq {rec.Seq} {rec.Path}: {string.Join(", ", diffs.Take(8))}");
                }

                truth.Advance(rec.Path, rec.Req, rec.Res);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
