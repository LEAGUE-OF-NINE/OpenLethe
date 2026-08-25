using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

// Boots the real app against the fixture's Postgres and a fixed JWT secret so
// tests can mint tokens the running server will accept. Extra settings land on
// top for tests that flip config switches (login gate, rate limits).
public class DbWebAppFactory(string connString, params (string Key, string Value)[] settings)
    : WebApplicationFactory<Program>
{
    public const string TestSecret = "integration-test-secret-integration-test-secret";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", connString);
        builder.UseSetting("Auth:JwtSecret", TestSecret);
        foreach (var (key, value) in settings) builder.UseSetting(key, value);
    }
}
