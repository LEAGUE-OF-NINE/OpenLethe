using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenLethe.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomMdCommonData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomMdCommonData",
                table: "accounts",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomMdCommonData",
                table: "accounts");
        }
    }
}
