using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

// /login/GetTermsOfUseStateAll must return the terms as ALREADY ACCEPTED
// (version 1, state 1) so the client skips the user-agreement prompt. It is a
// real handler (not the generic static registration, which returned an empty
// list) - DB-free, /login/ is auth-exempt.
public class GetTermsOfUseStateTests : IClassFixture<GetTermsOfUseStateTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder b) =>
            b.UseSetting("Auth:JwtSecret", "terms-test-secret-terms-test-secret-terms-test");
    }

    private readonly Factory _f;
    public GetTermsOfUseStateTests(Factory f) => _f = f;

    [Fact]
    public async Task ReturnsTermsAsAccepted()
    {
        var client = _f.CreateClient();

        var resp = await client.PostAsJsonAsync("/login/GetTermsOfUseStateAll",
            new { userAuth = new { }, parameters = new { uid = 0 } });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var list = doc.RootElement.GetProperty("result").GetProperty("termsOfUseStateList");
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(1, list[0].GetProperty("version").GetInt32());
        Assert.Equal(1, list[0].GetProperty("state").GetInt32()); // 1 = AGREE
    }
}
