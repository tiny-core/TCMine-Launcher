using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerConfigs",
                columns: table => new
                {
                    Uuid = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ModpackId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerConfigs", x => new { x.Uuid, x.ModpackId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerConfigs");
        }
    }
}
