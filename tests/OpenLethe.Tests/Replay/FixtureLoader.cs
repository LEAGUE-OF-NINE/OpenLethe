using System.Text.Json.Nodes;

namespace OpenLethe.Tests.Replay;

public sealed record FixtureRecord(int Seq, string Path, JsonNode? Req, JsonNode? Res, int Status);

public static class FixtureLoader
{
    public static readonly IReadOnlyList<(string RunId, string File)> Runs = new[]
    {
        ("run1", "md-extreme-run.jsonl"),
        ("run2", "md-extreme-run-2.jsonl"),
    };

    /// Refraction Railway capture (docs/flows(2)), scoped to the /api/*Railway*
    /// flows. Separate from Runs because it drives a different replay + tracker.
    public static readonly IReadOnlyList<(string RunId, string File)> RailwayRuns = new[]
    {
        ("rr2", "railway-rr2-run.jsonl"),
    };

    /// Every committed fixture - what the secret guard has to cover.
    public static IEnumerable<(string RunId, string File)> All => Runs.Concat(RailwayRuns);

    public static string PathFor(string file) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", file);

    public static string Path => PathFor("md-extreme-run.jsonl");

    public static IReadOnlyList<FixtureRecord> Records() => Records("md-extreme-run.jsonl");

    public static IReadOnlyList<FixtureRecord> Records(string file)
    {
        var list = new List<FixtureRecord>();
        foreach (var line in File.ReadLines(PathFor(file)))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var o = JsonNode.Parse(line)!.AsObject();
            list.Add(new FixtureRecord(
                (int)o["seq"]!, (string)o["path"]!,
                o["req"], o["res"], o["status"] is null ? 200 : (int)o["status"]!));
        }
        return list;
    }
}
