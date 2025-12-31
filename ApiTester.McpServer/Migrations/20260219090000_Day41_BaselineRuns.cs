using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiTester.McpServer.Migrations
{
    /// <inheritdoc />
    public partial class Day41_BaselineRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BaselineRunId",
                table: "TestRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_BaselineRunId",
                table: "TestRuns",
                column: "BaselineRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestRuns_TestRuns_BaselineRunId",
                table: "TestRuns",
                column: "BaselineRunId",
                principalTable: "TestRuns",
                principalColumn: "RunId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestRuns_TestRuns_BaselineRunId",
                table: "TestRuns");

            migrationBuilder.DropIndex(
                name: "IX_TestRuns_BaselineRunId",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "BaselineRunId",
                table: "TestRuns");
        }
    }
}
