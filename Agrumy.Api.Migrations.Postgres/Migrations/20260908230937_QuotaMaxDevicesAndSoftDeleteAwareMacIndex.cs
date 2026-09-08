using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
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
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSensorPushAt",
                table: "deviceDiagnostic",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActiveMacAddress",
                table: "device",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                computedColumnSql: "(CASE WHEN NOT \"Deleted\" THEN \"MacAddress\" ELSE NULL END)",
                stored: true);

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
