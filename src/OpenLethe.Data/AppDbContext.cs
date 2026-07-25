using Microsoft.EntityFrameworkCore;

namespace OpenLethe.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var a = b.Entity<Account>();
        a.ToTable("accounts");
        a.HasKey(x => x.Id);
        a.HasIndex(x => x.Username).IsUnique();
        a.HasIndex(x => x.IngameId).IsUnique();
        a.Property(x => x.Username).IsRequired();

        // Every game-data column is jsonb.
        foreach (var name in new[]
        {
            nameof(Account.Formations), nameof(Account.Egos), nameof(Account.Personalities),
            nameof(Account.Announcers), nameof(Account.UserInfo), nameof(Account.CustomTheme),
            nameof(Account.CustomIdentities), nameof(Account.CustomEgos),
            nameof(Account.MdSaveInfo), nameof(Account.StorySaveInfo), nameof(Account.StoryMdSaveInfo),
            nameof(Account.RailwaySaveInfo), nameof(Account.RailwayNodeData), nameof(Account.RailwayBuffs),
            nameof(Account.ChapterState), nameof(Account.BossRaidSaveInfo),
        })
        {
            a.Property(name).HasColumnType("jsonb");
        }
    }
}
