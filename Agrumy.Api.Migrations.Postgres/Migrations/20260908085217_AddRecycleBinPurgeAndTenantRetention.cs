using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddRecycleBinPurgeAndTenantRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecycleBinRetentionDays",
                table: "tenant",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Purged",
                table: "deviceFarm",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PurgedAtUtc",
                table: "deviceFarm",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Purged",
                table: "device",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PurgedAtUtc",
                table: "device",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecycleBinRetentionDays",
                table: "tenant");

            migrationBuilder.DropColumn(
                name: "Purged",
                table: "deviceFarm");

            migrationBuilder.DropColumn(
                name: "PurgedAtUtc",
                table: "deviceFarm");

            migrationBuilder.DropColumn(
                name: "Purged",
                table: "device");

            migrationBuilder.DropColumn(
                name: "PurgedAtUtc",
                table: "device");
        }
    }
}
