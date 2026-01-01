using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiTester.McpServer.Migrations
{
    /// <inheritdoc />
    public partial class Day47_AuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Actor",
                table: "TestRuns",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentBaseUrl",
                table: "TestRuns",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentName",
                table: "TestRuns",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicySnapshotJson",
                table: "TestRuns",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Actor",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "EnvironmentBaseUrl",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "EnvironmentName",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "PolicySnapshotJson",
                table: "TestRuns");
        }
    }
}
