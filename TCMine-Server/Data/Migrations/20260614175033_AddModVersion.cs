using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "Mods",
                type: "TEXT",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Mods");
        }
    }
}
