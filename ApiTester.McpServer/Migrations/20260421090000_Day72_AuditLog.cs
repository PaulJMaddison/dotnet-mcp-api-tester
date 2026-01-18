using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiTester.McpServer.Migrations
{
    /// <inheritdoc />
    public partial class Day72_AuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    AuditEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.AuditEventId);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "OrganisationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OrganisationId_CreatedUtc",
                table: "AuditEvents",
                columns: new[] { "OrganisationId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OrganisationId_Action_CreatedUtc",
                table: "AuditEvents",
                columns: new[] { "OrganisationId", "Action", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");
        }
    }
}
