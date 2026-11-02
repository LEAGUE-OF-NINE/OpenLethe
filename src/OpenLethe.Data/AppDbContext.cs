using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OpenLethe.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    /// Every game-data column; each is jsonb.
    internal static readonly string[] JsonColumns =
    {
        nameof(Account.Formations), nameof(Account.Egos), nameof(Account.Personalities),
        nameof(Account.Announcers), nameof(Account.UserInfo), nameof(Account.CustomTheme),
        nameof(Account.CustomIdentities), nameof(Account.CustomEgos),
        nameof(Account.CustomEgoGifts), nameof(Account.CustomEgoGiftDropPools),
        nameof(Account.CustomMdBattles),
        nameof(Account.MdSaveInfo), nameof(Account.StorySaveInfo), nameof(Account.StoryMdSaveInfo),
        nameof(Account.RailwaySaveInfo),
        nameof(Account.ChapterState), nameof(Account.BossRaidSaveInfo),
        nameof(Account.Chapter10RpgSaveInfo),
    };

    protected override void OnModelCreating(ModelBuilder b)
    {
        var a = b.Entity<Account>();
        a.ToTable("accounts");
        a.HasKey(x => x.Id);
        a.HasIndex(x => x.Username).IsUnique();
        a.HasIndex(x => x.IngameId).IsUnique();
        // DB-generated so concurrent signups can't collide; BY DEFAULT (not ALWAYS)
        // because the dashboard's ingameid/update writes explicit values.
        a.Property(x => x.IngameId).UseIdentityByDefaultColumn();
        // Filtered: dev-login accounts share DiscordId == null.
        a.HasIndex(x => x.DiscordId).IsUnique().HasFilter("\"DiscordId\" IS NOT NULL");
        a.Property(x => x.Username).IsRequired();

        foreach (var name in JsonColumns) a.Property(name).HasColumnType("jsonb");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        DropUnchangedDocuments();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        DropUnchangedDocuments();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    /// jsonb doesn't store the text it was given: it reorders keys and respaces, so a
    /// document read back and re-serialized unchanged never string-equals what was loaded,
    /// and EF would rewrite it. Each rewrite of a large document leaves a dead TOAST copy
    /// behind for autovacuum, so unmark any document whose JSON content is the same.
    private void DropUnchangedDocuments()
    {
        foreach (var e in ChangeTracker.Entries<Account>())   // runs DetectChanges
        {
            if (e.State != EntityState.Modified) continue;
            foreach (var name in JsonColumns)
            {
                var p = e.Property(name);
                if (p.IsModified && SameJson(p.OriginalValue as string, p.CurrentValue as string))
                    p.IsModified = false;
            }
        }
    }

    // A null original is a scoped read's unloaded column (see HandlerContext): never equal,
    // so a write to it still reaches SaveAsync's guard and the database.
    private static bool SameJson(string? a, string? b)
    {
        if (a is null || b is null) return false;
        if (a == b) return true;
        try
        {
            using var x = JsonDocument.Parse(a);
            using var y = JsonDocument.Parse(b);
            return JsonElement.DeepEquals(x.RootElement, y.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
