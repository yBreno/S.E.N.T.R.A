using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPredictiveCity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Alerts",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Failure");

            migrationBuilder.CreateTable(
                name: "AgentPositionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Neighborhood = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPositionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentPositionLogs_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Neighborhood = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidents_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Neighborhood = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RiskScore = table.Column<double>(type: "float", nullable: true),
                    ExplanationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recommendations_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Type_CreatedAt",
                table: "Alerts",
                columns: new[] { "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentPositionLogs_AgentId_RecordedAt",
                table: "AgentPositionLogs",
                columns: new[] { "AgentId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_EquipmentId",
                table: "Incidents",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Neighborhood_OccurredAt",
                table: "Incidents",
                columns: new[] { "Neighborhood", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_OccurredAt",
                table: "Incidents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_EquipmentId",
                table: "Recommendations",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_Status",
                table: "Recommendations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_Type_Neighborhood_Status",
                table: "Recommendations",
                columns: new[] { "Type", "Neighborhood", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentPositionLogs");

            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "Recommendations");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_Type_CreatedAt",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Alerts");
        }
    }
}
