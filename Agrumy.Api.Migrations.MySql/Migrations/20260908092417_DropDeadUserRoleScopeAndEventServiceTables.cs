using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
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
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "eventService",
                columns: table => new
                {
                    IDEventService = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    EventID = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ServiceID = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "userRoleScope",
                columns: table => new
                {
                    IDRoleScope = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RoleScopeName = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userRoleScope", x => x.IDRoleScope);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
