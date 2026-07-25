using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

// Boots the real app against the fixture's Postgres and a fixed JWT secret so
// tests can mint tokens the running server will accept.
public sealed class DbWebAppFactory(string connString) : WebApplicationFactory<Program>
{
    public const string TestSecret = "integration-test-secret-integration-test-secret";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", connString);
        builder.UseSetting("Auth:JwtSecret", TestSecret);
    }
}
