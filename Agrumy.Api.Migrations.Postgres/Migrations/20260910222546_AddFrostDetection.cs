using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddFrostDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FrostCheckedAtUtc",
                table: "serverConfig",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FrostCloudinessMaxPercent",
                table: "serverConfig",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrostLookaheadHours",
                table: "serverConfig",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FrostPredicted",
                table: "serverConfig",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FrostPredictedHoursAhead",
                table: "serverConfig",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FrostTempThresholdC",
                table: "serverConfig",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FrostWindMaxMetersPerSecond",
                table: "serverConfig",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FrostCheckedAtUtc",
                table: "serverConfig");

            migrationBuilder.DropColumn(
                name: "FrostCloudinessMaxPercent",
                table: "serverConfig");

            migrationBuilder.DropColumn(
                name: "FrostLookaheadHours",
                table: "serverConfig");

            migrationBuilder.DropColumn(
                name: "FrostPredicted",
                table: "serverConfig");

            migrationBuilder.DropColumn(
                name: "FrostPredictedHoursAhead",
                table: "serverConfig");

            migrationBuilder.DropColumn(
                name: "FrostTempThresholdC",
                table: "serverConfig");

            migrationBuilder.DropColumn(
                name: "FrostWindMaxMetersPerSecond",
                table: "serverConfig");
        }
    }
}
