using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenLethe.Server.Auth;
using OpenLethe.Server.Locale;

// /misc/locale calls out to an OpenAI-compatible endpoint, so these cover the
// parts that answer without one: the captcha gate, the rate limiter, job lookup,
// and the two parsers between the model and us.
public class LocaleEndpointTests : IClassFixture<LocaleEndpointTests.Factory>
{
    public const string Secret = "locale-test-secret-locale-test-secret-locale!";

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder b) =>
            b.UseSetting("Auth:JwtSecret", Secret);
    }

    private readonly Factory _f;
    public LocaleEndpointTests(Factory f) => _f = f;

    private static string CaptchaCookie(string sub) =>
        "abuse_exemption=" + new JwtService(Secret, TimeSpan.FromHours(1)).MintCaptcha(sub);

    private static HttpRequestMessage Post(string url, string? cookie)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new { id = 1, skillData = Array.Empty<object>() }),
        };
        if (cookie is not null) req.Headers.Add("Cookie", cookie);
        return req;
    }

    [Fact]
    public async Task Locale_WithoutCaptchaCookie_Is422()
    {
        var resp = await _f.CreateClient().SendAsync(Post("/misc/locale", null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    // A plain login token is not a captcha token - the flag has to be set.
    [Fact]
    public async Task Locale_WithNonCaptchaToken_Is422()
    {
        var cookie = "abuse_exemption="
            + new JwtService(Secret, TimeSpan.FromHours(1)).Mint("someone");

        var resp = await _f.CreateClient().SendAsync(Post("/misc/locale", cookie));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task Locale_SecondRequestWithinTheWindow_Is429()
    {
        var client = _f.CreateClient();
        var cookie = CaptchaCookie($"rate_{Guid.NewGuid():N}");

        // First call passes the gate; it then fails at the API call (no key
        // configured), which is fine - what matters is that it consumed the budget.
        var first = await client.SendAsync(Post("/misc/locale", cookie));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);

        var second = await client.SendAsync(Post("/misc/locale", cookie));
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task Submit_QueuesAJob_AndResultReportsIt()
    {
        var client = _f.CreateClient();

        var submit = await client.SendAsync(Post("/misc/locale/submit", CaptchaCookie($"job_{Guid.NewGuid():N}")));
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        using var doc = JsonDocument.Parse(await submit.Content.ReadAsStringAsync());
        var jobId = doc.RootElement.GetProperty("jobId").GetString();
        Assert.False(string.IsNullOrEmpty(jobId));

        var result = await client.GetAsync($"/misc/locale/result/{jobId}");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        using var got = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        Assert.Equal(jobId, got.RootElement.GetProperty("jobId").GetString());
        // No OPENAI_API_KEY here, so the job is pending/processing or already failed -
        // never completed. Status must serialize as one of the camelCase names.
        Assert.Contains(got.RootElement.GetProperty("status").GetString(),
            new[] { "pending", "processing", "failed" });
    }

    // A bad body is the caller's fault: 400, and it must not burn the rate-limit
    // budget on the way out.
    [Fact]
    public async Task Locale_MalformedBody_Is400_AndKeepsTheBudget()
    {
        var client = _f.CreateClient();
        var cookie = CaptchaCookie($"malformed_{Guid.NewGuid():N}");

        var bad = new HttpRequestMessage(HttpMethod.Post, "/misc/locale")
        {
            Content = new StringContent("{not json", System.Text.Encoding.UTF8, "application/json"),
        };
        bad.Headers.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(bad)).StatusCode);

        var good = await client.SendAsync(Post("/misc/locale", cookie));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, good.StatusCode);
    }

    [Fact]
    public async Task Result_UnknownJob_Is404()
    {
        var resp = await _f.CreateClient().GetAsync($"/misc/locale/result/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}

public class LocaleParsingTests
{
    [Fact]
    public void ParseJsonBody_AcceptsPlainJson()
    {
        var value = LocaleEndpoints.ParseJsonBody("""{"id":5}""");

        Assert.Equal(5, value.GetProperty("id").GetInt32());
    }

    // Models wrap answers in fences and chatter; the outermost braces still win.
    [Fact]
    public void ParseJsonBody_DigsJsonOutOfProse()
    {
        var value = LocaleEndpoints.ParseJsonBody("Sure!\n```json\n{\"id\": 7}\n```\nHope that helps.");

        Assert.Equal(7, value.GetProperty("id").GetInt32());
    }

    [Fact]
    public void ParseJsonBody_ThrowsWhenThereIsNoJson()
    {
        Assert.Throws<InvalidOperationException>(() => LocaleEndpoints.ParseJsonBody("no json here"));
    }

    [Fact]
    public void ParseModularDoc_SplitsSectionsPerFunction()
    {
        const string html = """
            <html><body>
            <textarea id="id_text" rows="20">intro text here
            **Value Acquisition Functions**
            ### **GetPower** (int)
            returns power
            more power
            ### **GetCount** (int)
            returns count
            **Condition Functions**
            ### **Ignored**
            should not appear
            </textarea>
            </body></html>
            """;

        var doc = LocaleEndpoints.ParseModularDoc(html);

        Assert.Contains("intro text here", doc[""]);
        Assert.Contains("returns power", doc["GetPower"]);
        Assert.Contains("more power", doc["GetPower"]);
        Assert.Contains("returns count", doc["GetCount"]);
        Assert.DoesNotContain("Ignored", doc.Keys);          // stops at Condition Functions
        Assert.DoesNotContain("returns count", doc["GetPower"]); // sections don't bleed
    }

    // These two ran green before against the REAL rentry.co, which proved nothing:
    // a network-backed assertion cannot tell "did not fetch" from "fetch worked".
    // The factory below makes every outbound call fail, so the prompt has to stand
    // on its own.
    private sealed class ExplodingHttpClientFactory : IHttpClientFactory
    {
        public int Calls;

        public HttpClient CreateClient(string name)
        {
            Calls++;
            return new HttpClient(new ExplodingHandler());
        }

        private sealed class ExplodingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken ct) =>
                throw new HttpRequestException("network is down");
        }
    }

    private static Microsoft.Extensions.Caching.Memory.IMemoryCache NewCache() =>
        new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());

    private static JsonElement Skill(string scriptName) => JsonDocument.Parse(
        $$"""{"id":1,"skillData":[{"abilityScriptList":[{"scriptName":"{{scriptName}}"}],"coinList":[]}]}""")
        .RootElement;

    [Fact]
    public async Task SystemPrompt_DoesNotFetchAtAll_WhenTheSkillUsesNoDsl()
    {
        var http = new ExplodingHttpClientFactory();

        var prompt = await LocaleEndpoints.SystemPromptAsync(
            http, NewCache(), Skill("EmptyBody"), CancellationToken.None);

        Assert.Equal(0, http.Calls);            // the fetch was skipped, not merely survived
        Assert.Contains("# Modular Documentation:", prompt);
        Assert.Contains("**input**", prompt);   // few-shot examples still assembled
    }

    [Fact]
    public async Task SystemPrompt_DegradesToNoDocs_WhenRentryIsUnreachable()
    {
        var http = new ExplodingHttpClientFactory();

        var prompt = await LocaleEndpoints.SystemPromptAsync(
            http, NewCache(), Skill("Modular/Something"), CancellationToken.None);

        Assert.Equal(1, http.Calls);            // it did try
        Assert.Contains("# Examples", prompt);  // and still produced a usable prompt
    }

    [Fact]
    public void ParseModularDoc_ThrowsWhenTheTextareaIsMissing()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LocaleEndpoints.ParseModularDoc("<html><body>nothing</body></html>"));
    }

    [Fact]
    public void ParseModularDoc_ThrowsWhenTheSplitMarkerIsMissing()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LocaleEndpoints.ParseModularDoc("<textarea id=\"id_text\">no marker</textarea>"));
    }
}

// The few-shot machinery: without a populated index every prompt would ship with
// zero examples and the model would have nothing to imitate.
public class SkillIndexTests
{
    [Fact]
    public void Index_IsBuiltFromTheBundledSkillData()
    {
        Assert.True(SkillIndex.SkillJson.Count > 2500, $"only {SkillIndex.SkillJson.Count} skills");
        Assert.True(SkillIndex.LocaleJson.Count > 2500, $"only {SkillIndex.LocaleJson.Count} locales");
        Assert.NotEmpty(SkillIndex.AbilityIndex);
        Assert.NotEmpty(SkillIndex.Tags);
    }

    [Fact]
    public void FindSkillsUsingAbility_OnlyReturnsIdsThatHaveBothHalves()
    {
        var (script, categories) = SkillIndex.AbilityIndex.First(e => e.Value.Count > 0);
        var keyword = categories.Keys.First();

        var ids = SkillIndex.FindSkillsUsingAbility(
            new SkillAbility(script, keyword == SkillIndex.NoKeyword ? null : keyword),
            checkBuffKeyword: true);

        Assert.All(ids, id =>
        {
            Assert.True(SkillIndex.SkillJson.ContainsKey(id));
            Assert.True(SkillIndex.LocaleJson.ContainsKey(id));
        });
        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public void FindSimilarSkills_ReturnsTenExamples_AndNeverTheInputItself()
    {
        var id = SkillIndex.SkillJson.Keys.First();
        var skill = JsonSerializer.Deserialize<CompactSkill>(
            SkillIndex.SkillJson[id], new JsonSerializerOptions { IncludeFields = true })!;

        var similar = SkillIndex.FindSimilarSkills(skill);

        Assert.Equal(10, similar.Count);
        Assert.DoesNotContain(id, similar);
        Assert.Equal(similar.Distinct().Count(), similar.Count);
    }

    // The compact form is what the prompt actually carries - it must keep the
    // ability scripts and drop everything else.
    [Fact]
    public void CompactSkill_KeepsAbilityScripts_AndDropsTheRest()
    {
        var id = SkillIndex.SkillJson.First(e => e.Value.Contains("scriptName")).Key;
        var json = SkillIndex.SkillJson[id];

        Assert.Contains("\"skillData\"", json);
        Assert.Contains("\"scriptName\"", json);
        Assert.DoesNotContain("\"skillTier\"", json);
        Assert.DoesNotContain("\"skillMotion\"", json);
    }
}
