using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenLethe.Data;

// Used only by `dotnet ef` at design time; never at runtime.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=openlethe_design;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }
}
