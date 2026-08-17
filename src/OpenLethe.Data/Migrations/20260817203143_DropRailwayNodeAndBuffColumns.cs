using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenLethe.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropRailwayNodeAndBuffColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RailwayBuffs",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "RailwayNodeData",
                table: "accounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RailwayBuffs",
                table: "accounts",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RailwayNodeData",
                table: "accounts",
                type: "jsonb",
                nullable: false,
                defaultValue: "");
        }
    }
}
