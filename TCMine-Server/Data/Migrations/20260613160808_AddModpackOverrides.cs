using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModpackOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasOverrides",
                table: "Modpacks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasOverrides",
                table: "Modpacks");
        }
    }
}
