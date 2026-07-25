using Microsoft.EntityFrameworkCore;
using OpenLethe.Data;

public class AccountModelTests
{
    private static AppDbContext BuildContext()
    {
        // No connection needed - we only inspect model metadata, never query.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused;Database=unused")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Account_MapsToAccountsTable_WithUniqueUsername()
    {
        using var ctx = BuildContext();
        var et = ctx.Model.FindEntityType(typeof(Account))!;

        Assert.Equal("accounts", et.GetTableName());
        Assert.Contains(et.GetIndexes(), i =>
            i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == nameof(Account.Username));
    }

    [Fact]
    public void GameDataColumns_AreJsonb()
    {
        using var ctx = BuildContext();
        var et = ctx.Model.FindEntityType(typeof(Account))!;

        Assert.Equal("jsonb", et.FindProperty(nameof(Account.MdSaveInfo))!.GetColumnType());
        Assert.Equal("jsonb", et.FindProperty(nameof(Account.Egos))!.GetColumnType());
    }
}
