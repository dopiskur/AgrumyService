using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
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
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FrostCloudinessMaxPercent",
                table: "serverConfig",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrostLookaheadHours",
                table: "serverConfig",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FrostPredicted",
                table: "serverConfig",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FrostPredictedHoursAhead",
                table: "serverConfig",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FrostTempThresholdC",
                table: "serverConfig",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FrostWindMaxMetersPerSecond",
                table: "serverConfig",
                type: "double",
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
