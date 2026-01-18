using System;
using ApiTester.McpServer.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiTester.McpServer.Migrations
{
    /// <inheritdoc />
    public partial class Day70_OrgUsersRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Organisations",
                columns: table => new
                {
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organisations", x => x.OrganisationId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Memberships",
                columns: table => new
                {
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memberships", x => new { x.OrganisationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_Memberships_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "OrganisationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Memberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_Slug",
                table: "Organisations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalId",
                table: "Users",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_UserId",
                table: "Memberships",
                column: "UserId");

            migrationBuilder.InsertData(
                table: "Organisations",
                columns: new[] { "OrganisationId", "Name", "Slug", "CreatedUtc" },
                values: new object[]
                {
                    OrgDefaults.DefaultOrganisationId,
                    OrgDefaults.DefaultOrganisationName,
                    OrgDefaults.DefaultOrganisationSlug,
                    new DateTime(2026, 4, 19, 9, 0, 0, DateTimeKind.Utc)
                });

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: OrgDefaults.DefaultOrganisationId);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId",
                table: "TestRuns",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: OrgDefaults.DefaultOrganisationId);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganisationId",
                table: "Projects",
                column: "OrganisationId");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerKey_ProjectKey",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganisationId_ProjectKey",
                table: "Projects",
                columns: new[] { "OrganisationId", "ProjectKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_OrganisationId",
                table: "TestRuns",
                column: "OrganisationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Organisations_OrganisationId",
                table: "Projects",
                column: "OrganisationId",
                principalTable: "Organisations",
                principalColumn: "OrganisationId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TestRuns_Organisations_OrganisationId",
                table: "TestRuns",
                column: "OrganisationId",
                principalTable: "Organisations",
                principalColumn: "OrganisationId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Organisations_OrganisationId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_TestRuns_Organisations_OrganisationId",
                table: "TestRuns");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OrganisationId_ProjectKey",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OrganisationId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_TestRuns_OrganisationId",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                table: "TestRuns");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerKey_ProjectKey",
                table: "Projects",
                columns: new[] { "OwnerKey", "ProjectKey" },
                unique: true);

            migrationBuilder.DropTable(
                name: "Memberships");

            migrationBuilder.DropTable(
                name: "Organisations");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
