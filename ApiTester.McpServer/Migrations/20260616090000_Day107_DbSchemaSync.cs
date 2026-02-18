using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiTester.McpServer.Migrations
{
    /// <inheritdoc />
    public partial class Day107_DbSchemaSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpenApiSpecs_Projects_ApiTester.McpServer.Persistence.Entities.OpenApiSpecEntity_ProjectId",
                table: "OpenApiSpecs");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Projects_TempId_TempId1",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TempId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TempId1",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ApiTester.McpServer.Persistence.Entities.OpenApiSpecEntity",
                table: "OpenApiSpecs");

            migrationBuilder.AddColumn<string>(
                name: "OrgSettingsJson",
                table: "Organisations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedactionRulesJson",
                table: "Organisations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionDays",
                table: "Organisations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiInsights",
                columns: table => new
                {
                    InsightId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    JsonPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInsights", x => x.InsightId);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    KeyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Scopes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Hash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.KeyId);
                    table.ForeignKey(
                        name: "FK_ApiKeys_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "OrganisationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApiKeys_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BaselineRuns",
                columns: table => new
                {
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SetUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaselineRuns", x => new { x.ProjectId, x.OperationId });
                    table.ForeignKey(
                        name: "FK_BaselineRuns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BaselineRuns_TestRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "TestRuns",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Environments",
                columns: table => new
                {
                    EnvironmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Environments", x => x.EnvironmentId);
                    table.ForeignKey(
                        name: "FK_Environments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedDocs",
                columns: table => new
                {
                    DocsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedDocs", x => x.DocsId);
                    table.ForeignKey(
                        name: "FK_GeneratedDocs_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "OrganisationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GeneratedDocs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Renews = table.Column<bool>(type: "bit", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProjectsUsed = table.Column<int>(type: "int", nullable: false),
                    RunsUsed = table.Column<int>(type: "int", nullable: false),
                    AiCallsUsed = table.Column<int>(type: "int", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.OrganisationId);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "OrganisationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiInsights_OrganisationId_OperationId",
                table: "AiInsights",
                columns: new[] { "OrganisationId", "OperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInsights_OrganisationId_ProjectId_RunId",
                table: "AiInsights",
                columns: new[] { "OrganisationId", "ProjectId", "RunId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_OrganisationId",
                table: "ApiKeys",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_Prefix",
                table: "ApiKeys",
                column: "Prefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_UserId",
                table: "ApiKeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BaselineRuns_RunId",
                table: "BaselineRuns",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_Environments_OwnerKey_ProjectId_Name",
                table: "Environments",
                columns: new[] { "OwnerKey", "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Environments_ProjectId",
                table: "Environments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocs_OrganisationId",
                table: "GeneratedDocs",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocs_OrganisationId_ProjectId",
                table: "GeneratedDocs",
                columns: new[] { "OrganisationId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocs_ProjectId",
                table: "GeneratedDocs",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_OpenApiSpecs_Projects_ProjectId",
                table: "OpenApiSpecs",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpenApiSpecs_Projects_ProjectId",
                table: "OpenApiSpecs");

            migrationBuilder.DropTable(
                name: "AiInsights");

            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "BaselineRuns");

            migrationBuilder.DropTable(
                name: "Environments");

            migrationBuilder.DropTable(
                name: "GeneratedDocs");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "OrgSettingsJson",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "RedactionRulesJson",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "RetentionDays",
                table: "Organisations");

            migrationBuilder.AddColumn<int>(
                name: "TempId",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TempId1",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "ApiTester.McpServer.Persistence.Entities.OpenApiSpecEntity",
                table: "OpenApiSpecs",
                type: "int",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Projects_TempId_TempId1",
                table: "Projects",
                columns: new[] { "TempId", "TempId1" });

            migrationBuilder.AddForeignKey(
                name: "FK_OpenApiSpecs_Projects_ApiTester.McpServer.Persistence.Entities.OpenApiSpecEntity_ProjectId",
                table: "OpenApiSpecs",
                columns: new[] { "ApiTester.McpServer.Persistence.Entities.OpenApiSpecEntity", "ProjectId" },
                principalTable: "Projects",
                principalColumns: new[] { "TempId", "TempId1" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}
