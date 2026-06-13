using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMine_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendedRam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecommendedRamMb",
                table: "Modpacks",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecommendedRamMb",
                table: "Modpacks");
        }
    }
}
