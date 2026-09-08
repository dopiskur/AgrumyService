using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class DropDeadUserRoleScopeAndEventServiceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_userRole_userRoleScope_RoleScopeID",
                table: "userRole");

            migrationBuilder.DropTable(
                name: "eventService");

            migrationBuilder.DropTable(
                name: "userRoleScope");

            migrationBuilder.DropIndex(
                name: "IX_userRole_RoleScopeID",
                table: "userRole");

            migrationBuilder.DropColumn(
                name: "RoleScopeID",
                table: "userRole");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoleScopeID",
                table: "userRole",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "eventService",
                columns: table => new
                {
                    IDEventService = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EventID = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    ServiceID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventService", x => x.IDEventService);
                    table.ForeignKey(
                        name: "FK_eventService_deviceTypeService_ServiceID",
                        column: x => x.ServiceID,
                        principalTable: "deviceTypeService",
                        principalColumn: "IDDeviceTypeService");
                    table.ForeignKey(
                        name: "FK_eventService_eventType_EventID",
                        column: x => x.EventID,
                        principalTable: "eventType",
                        principalColumn: "IDEventType");
                });

            migrationBuilder.CreateTable(
                name: "userRoleScope",
                columns: table => new
                {
                    IDRoleScope = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleScopeName = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userRoleScope", x => x.IDRoleScope);
                });

            migrationBuilder.CreateIndex(
                name: "IX_userRole_RoleScopeID",
                table: "userRole",
                column: "RoleScopeID");

            migrationBuilder.CreateIndex(
                name: "IX_eventService_EventID",
                table: "eventService",
                column: "EventID");

            migrationBuilder.CreateIndex(
                name: "IX_eventService_ServiceID",
                table: "eventService",
                column: "ServiceID");

            migrationBuilder.AddForeignKey(
                name: "FK_userRole_userRoleScope_RoleScopeID",
                table: "userRole",
                column: "RoleScopeID",
                principalTable: "userRoleScope",
                principalColumn: "IDRoleScope");
        }
    }
}
