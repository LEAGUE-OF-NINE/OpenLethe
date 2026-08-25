using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenLethe.Tests.Replay;

public class FixtureGuardTests
{
    // Runs unconditionally — leak prevention must not depend on Docker.
    // This test embeds NO real identifier: it proves each fixture is scrubbed by
    // (1) the absence of secret-SHAPED patterns and (2) positively asserting every
    // identity field equals its synthetic constant.
    [Theory]
    [MemberData(nameof(FixtureFiles))]
    public void Fixture_ContainsNoSecrets(string file)
    {
        var text = File.ReadAllText(FixtureLoader.PathFor(file));

        // Secret-shaped patterns (not real values) must be absent.
        var patterns = new (string name, Regex rx)[]
        {
            ("steam ticket hex",   new Regex("14000000[0-9A-Fa-f]{16,}")),
            ("email",              new Regex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")),
            ("uuid",               new Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")),
            ("account-link field", new Regex("steamToken|google_account|apple_account|auth_code")),
        };
        foreach (var (name, rx) in patterns)
            Assert.False(rx.IsMatch(text), $"{file} leaks {name}");

        // Positive: every identity field is scrubbed to its synthetic constant, so no
        // real uid/public_uid can survive — without naming the real values here.
        foreach (var rec in FixtureLoader.Records(file))
        {
            if (rec.Req?["userAuth"] is JsonObject ua)
            {
                Assert.Equal("", (string?)ua["authCode"]);
                Assert.Equal(1, (int?)ua["uid"]);
            }
            if (rec.Res?["result"]?["profile"]?["public_uid"] is JsonNode pub)
                Assert.Equal("V000000000", (string?)pub);
        }
    }

    public static IEnumerable<object[]> FixtureFiles() =>
        FixtureLoader.All.Select(r => new object[] { r.File });
}
