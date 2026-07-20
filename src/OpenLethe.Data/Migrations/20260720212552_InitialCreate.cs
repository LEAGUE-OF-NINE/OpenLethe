using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenLethe.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    IngameId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Formations = table.Column<string>(type: "jsonb", nullable: false),
                    Egos = table.Column<string>(type: "jsonb", nullable: false),
                    Personalities = table.Column<string>(type: "jsonb", nullable: false),
                    Announcers = table.Column<string>(type: "jsonb", nullable: false),
                    UserInfo = table.Column<string>(type: "jsonb", nullable: false),
                    CustomTheme = table.Column<string>(type: "jsonb", nullable: false),
                    CustomIdentities = table.Column<string>(type: "jsonb", nullable: false),
                    CustomEgos = table.Column<string>(type: "jsonb", nullable: false),
                    MdSaveInfo = table.Column<string>(type: "jsonb", nullable: false),
                    StorySaveInfo = table.Column<string>(type: "jsonb", nullable: false),
                    StoryMdSaveInfo = table.Column<string>(type: "jsonb", nullable: false),
                    RailwaySaveInfo = table.Column<string>(type: "jsonb", nullable: false),
                    RailwayNodeData = table.Column<string>(type: "jsonb", nullable: false),
                    RailwayBuffs = table.Column<string>(type: "jsonb", nullable: false),
                    ChapterState = table.Column<string>(type: "jsonb", nullable: false),
                    BossRaidSaveInfo = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_IngameId",
                table: "accounts",
                column: "IngameId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Username",
                table: "accounts",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
