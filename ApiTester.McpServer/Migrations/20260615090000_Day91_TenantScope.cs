using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiTester.McpServer.Migrations;

public partial class Day91_TenantScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "TenantId",
            table: "Projects",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

        migrationBuilder.AddColumn<Guid>(
            name: "TenantId",
            table: "TestRuns",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

        migrationBuilder.AddColumn<Guid>(
            name: "TenantId",
            table: "OpenApiSpecs",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

        migrationBuilder.Sql("UPDATE Projects SET TenantId = OrganisationId WHERE TenantId = '11111111-1111-1111-1111-111111111111';");
        migrationBuilder.Sql("UPDATE TestRuns SET TenantId = OrganisationId WHERE TenantId = '11111111-1111-1111-1111-111111111111';");
        migrationBuilder.Sql(@"
UPDATE OpenApiSpecs
SET TenantId = Projects.TenantId
FROM OpenApiSpecs
INNER JOIN Projects ON OpenApiSpecs.ProjectId = Projects.ProjectId
WHERE OpenApiSpecs.TenantId = '11111111-1111-1111-1111-111111111111';
");

        migrationBuilder.DropIndex(
            name: "IX_Projects_OrganisationId_ProjectKey",
            table: "Projects");

        migrationBuilder.CreateIndex(
            name: "IX_Projects_TenantId",
            table: "Projects",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_Projects_TenantId_ProjectKey",
            table: "Projects",
            columns: new[] { "TenantId", "ProjectKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TestRuns_TenantId",
            table: "TestRuns",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_OpenApiSpecs_TenantId",
            table: "OpenApiSpecs",
            column: "TenantId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_OpenApiSpecs_TenantId",
            table: "OpenApiSpecs");

        migrationBuilder.DropIndex(
            name: "IX_Projects_TenantId",
            table: "Projects");

        migrationBuilder.DropIndex(
            name: "IX_Projects_TenantId_ProjectKey",
            table: "Projects");

        migrationBuilder.DropIndex(
            name: "IX_TestRuns_TenantId",
            table: "TestRuns");

        migrationBuilder.CreateIndex(
            name: "IX_Projects_OrganisationId_ProjectKey",
            table: "Projects",
            columns: new[] { "OrganisationId", "ProjectKey" },
            unique: true);

        migrationBuilder.DropColumn(
            name: "TenantId",
            table: "OpenApiSpecs");

        migrationBuilder.DropColumn(
            name: "TenantId",
            table: "Projects");

        migrationBuilder.DropColumn(
            name: "TenantId",
            table: "TestRuns");
    }
}
