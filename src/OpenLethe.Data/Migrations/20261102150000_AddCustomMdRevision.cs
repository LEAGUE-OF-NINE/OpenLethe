using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenLethe.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomMdRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CustomMdRevision",
                table: "accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // Accounts that uploaded before the stamp existed: give them one, or the server would
            // read 0 as "nothing uploaded" and skip their custom content.
            migrationBuilder.Sql("""
                UPDATE accounts SET "CustomMdRevision" = 1
                WHERE "CustomTheme" NOT IN ('{}', '[]', 'null')
                   OR "CustomEgoGifts" NOT IN ('{}', '[]', 'null')
                   OR "CustomEgoGiftDropPools" NOT IN ('{}', '[]', 'null')
                   OR "CustomMdBattles" NOT IN ('{}', '[]', 'null')
                   OR "CustomMdCommonData" NOT IN ('{}', '[]', 'null');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomMdRevision",
                table: "accounts");
        }
    }
}
