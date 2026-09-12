using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
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
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncIntervalDays",
                table: "tenantSatelliteConfig",
                type: "integer",
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
