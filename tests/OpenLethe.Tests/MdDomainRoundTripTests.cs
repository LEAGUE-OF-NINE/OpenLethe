using System.Text.Json;
using System.Text.Json.Nodes;
using OpenLethe.Server.MirrorDungeon.Mapping;
using OpenLethe.Server.Wire;
using OpenLethe.Tests.Replay;

namespace OpenLethe.Tests;

// Mapper-fidelity gate. NOT Docker-gated: pure in-memory (de)serialization over the
// committed fixtures. For every captured MD save in BOTH runs, mapping wire->domain->wire
// must reproduce the save byte-for-byte (under the shared serializer, which cancels out
// deserialize-normalization so only the mapper is under test).
public class MdDomainRoundTripTests
{
    private static string Canonical(MirrorOriginSaveInfo w) =>
        JsonSerializer.Serialize(w, global::PacketJson.Options);

    // Every full save (result.saveInfo/saveinfo) plus every bare currentInfo (wrapped into
    // a save shell so the CurrentInfo mapping is exercised on states only bare-currentInfo
    // endpoints produce), across both fixtures.
    public static IEnumerable<(string RunId, int Seq, string Kind, MirrorOriginSaveInfo Save)> MdSaveCorpus()
    {
        foreach (var (runId, file) in FixtureLoader.Runs)
        {
            foreach (var rec in FixtureLoader.Records(file))
            {
                var result = rec.Res?["result"] as JsonObject;
                if (result is null) continue;

                var full = result["saveInfo"] ?? result["saveinfo"];
                if (full is JsonObject fo)
                {
                    var save = fo.Deserialize<MirrorOriginSaveInfo>(global::PacketJson.Options)!;
                    yield return (runId, rec.Seq, "saveInfo", save);
                }
                else if (result["currentInfo"] is JsonObject ci)
                {
                    var shell = new JsonObject { ["currentInfo"] = ci.DeepClone() };
                    var save = shell.Deserialize<MirrorOriginSaveInfo>(global::PacketJson.Options)!;
                    yield return (runId, rec.Seq, "currentInfo", save);
                }
            }
        }
    }

    [Fact]
    public void RoundTrip_EveryCapturedSave_IsByteIdentical()
    {
        var corpus = MdSaveCorpus().ToList();
        Assert.NotEmpty(corpus); // guard: extractor must actually find saves

        var failures = new List<string>();
        foreach (var (runId, seq, kind, save) in corpus)
        {
            var before = Canonical(save);
            var after = Canonical(WireMapper.ToWire(WireMapper.ToDomain(save)));
            if (before != after)
                failures.Add($"[{runId}] seq {seq} ({kind}): round-trip diverged");
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
    }
}
