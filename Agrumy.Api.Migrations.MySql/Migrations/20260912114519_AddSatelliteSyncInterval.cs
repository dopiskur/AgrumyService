using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddSatelliteSyncInterval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAutoSyncUtc",
                table: "tenantSatelliteConfig",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncIntervalDays",
                table: "tenantSatelliteConfig",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastAutoSyncUtc",
                table: "tenantSatelliteConfig");

            migrationBuilder.DropColumn(
                name: "SyncIntervalDays",
                table: "tenantSatelliteConfig");
        }
    }
}
