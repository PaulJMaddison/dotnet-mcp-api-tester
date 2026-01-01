using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiTester.McpServer.Migrations
{
    /// <inheritdoc />
    public partial class Day43_SpecHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OpenApiSpecs_ProjectId",
                table: "OpenApiSpecs");

            migrationBuilder.AddColumn<Guid>(
                name: "SpecId",
                table: "TestRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecHash",
                table: "OpenApiSpecs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_SpecId",
                table: "TestRuns",
                column: "SpecId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenApiSpecs_ProjectId",
                table: "OpenApiSpecs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenApiSpecs_ProjectId_SpecHash",
                table: "OpenApiSpecs",
                columns: new[] { "ProjectId", "SpecHash" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TestRuns_OpenApiSpecs_SpecId",
                table: "TestRuns",
                column: "SpecId",
                principalTable: "OpenApiSpecs",
                principalColumn: "SpecId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestRuns_OpenApiSpecs_SpecId",
                table: "TestRuns");

            migrationBuilder.DropIndex(
                name: "IX_TestRuns_SpecId",
                table: "TestRuns");

            migrationBuilder.DropIndex(
                name: "IX_OpenApiSpecs_ProjectId",
                table: "OpenApiSpecs");

            migrationBuilder.DropIndex(
                name: "IX_OpenApiSpecs_ProjectId_SpecHash",
                table: "OpenApiSpecs");

            migrationBuilder.DropColumn(
                name: "SpecId",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "SpecHash",
                table: "OpenApiSpecs");

            migrationBuilder.CreateIndex(
                name: "IX_OpenApiSpecs_ProjectId",
                table: "OpenApiSpecs",
                column: "ProjectId",
                unique: true);
        }
    }
}
