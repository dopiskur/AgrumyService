using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class QuotaMaxDevicesAndSoftDeleteAwareMacIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "MacAddress_TenantID_UNIQUE",
                table: "device");

            migrationBuilder.AddColumn<int>(
                name: "MaxDevices",
                table: "tenantConfigQuota",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSensorPushAt",
                table: "deviceDiagnostic",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActiveMacAddress",
                table: "device",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                computedColumnSql: "(CASE WHEN `Deleted` = 0 THEN `MacAddress` ELSE NULL END)",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ActiveMacAddress_TenantID_UNIQUE",
                table: "device",
                columns: new[] { "ActiveMacAddress", "TenantID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ActiveMacAddress_TenantID_UNIQUE",
                table: "device");

            migrationBuilder.DropColumn(
                name: "ActiveMacAddress",
                table: "device");

            migrationBuilder.DropColumn(
                name: "MaxDevices",
                table: "tenantConfigQuota");

            migrationBuilder.DropColumn(
                name: "LastSensorPushAt",
                table: "deviceDiagnostic");

            migrationBuilder.CreateIndex(
                name: "MacAddress_TenantID_UNIQUE",
                table: "device",
                columns: new[] { "MacAddress", "TenantID" },
                unique: true);
        }
    }
}
