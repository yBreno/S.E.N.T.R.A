using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Users] WHERE [Role] NOT IN ('Admin','OperationManager','CentralOperator','MaintenanceTechnician','Agent'))
                    THROW 51010, 'Cargos legados desconhecidos. Revise antes de atualizar; nenhuma conversao automatica sera feita.', 1;
                IF EXISTS (SELECT 1 FROM [Users]) AND NOT EXISTS (SELECT 1 FROM [Users] WHERE [Role]='Admin')
                    THROW 51011, 'Existem contas sem administrador. Provisione um Admin por procedimento privado autorizado antes da migration.', 1;
                """);
            migrationBuilder.AddColumn<long>(
                name: "AuthVersion",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "UserAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PreviousRole = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    NewRole = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAuditEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAuditEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_AuthVersion",
                table: "Users",
                sql: "[AuthVersion] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Revision",
                table: "Users",
                sql: "[Revision] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserAuditEvents_ActorUserId",
                table: "UserAuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAuditEvents_UserId_CreatedAt_Id",
                table: "UserAuditEvents",
                columns: new[] { "UserId", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAuditEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_AuthVersion",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Revision",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AuthVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Users");
        }
    }
}
