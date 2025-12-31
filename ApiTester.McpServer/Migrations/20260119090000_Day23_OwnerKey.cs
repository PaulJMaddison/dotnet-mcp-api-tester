using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiTester.McpServer.Migrations
{
    /// <inheritdoc />
    public partial class Day23_OwnerKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_ProjectKey",
                table: "Projects");

            migrationBuilder.AddColumn<string>(
                name: "OwnerKey",
                table: "Projects",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "default");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerKey",
                table: "Projects",
                column: "OwnerKey");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerKey_ProjectKey",
                table: "Projects",
                columns: new[] { "OwnerKey", "ProjectKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerKey",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerKey_ProjectKey",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OwnerKey",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectKey",
                table: "Projects",
                column: "ProjectKey",
                unique: true);
        }
    }
}
