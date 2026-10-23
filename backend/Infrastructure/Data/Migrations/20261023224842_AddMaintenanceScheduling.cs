using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nao escolhe nem exclui ordens historicas: dados legados conflitantes exigem revisao.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT EquipmentId FROM MaintenanceRecords
                    WHERE Status IN ('Open', 'Scheduled') GROUP BY EquipmentId HAVING COUNT(*) > 1)
                    THROW 51001, 'Existem ordens pendentes duplicadas. Revise os registros antes de aplicar esta migration.', 1;
                """);
            migrationBuilder.AlterColumn<DateTime>(
                name: "InitDate",
                table: "MaintenanceRecords",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "MaintenanceRecords",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Corrective");

            migrationBuilder.AddColumn<Guid>(
                name: "RecommendationId",
                table: "MaintenanceRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledDate",
                table: "MaintenanceRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_EquipmentId",
                table: "MaintenanceRecords",
                column: "EquipmentId",
                unique: true,
                filter: "[Status] IN ('Open', 'Scheduled')");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRecords_RecommendationId",
                table: "MaintenanceRecords",
                column: "RecommendationId",
                unique: true,
                filter: "[RecommendationId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRecords_Recommendations_RecommendationId",
                table: "MaintenanceRecords",
                column: "RecommendationId",
                principalTable: "Recommendations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRecords_Recommendations_RecommendationId",
                table: "MaintenanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRecords_EquipmentId",
                table: "MaintenanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRecords_RecommendationId",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "RecommendationId",
                table: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "MaintenanceRecords");

            migrationBuilder.AlterColumn<DateTime>(
                name: "InitDate",
                table: "MaintenanceRecords",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
