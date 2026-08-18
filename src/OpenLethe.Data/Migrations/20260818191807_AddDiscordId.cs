using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenLethe.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscordId",
                table: "accounts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_DiscordId",
                table: "accounts",
                column: "DiscordId",
                unique: true,
                filter: "\"DiscordId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_accounts_DiscordId",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "DiscordId",
                table: "accounts");
        }
    }
}
