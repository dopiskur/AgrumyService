using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddOutdoorWeatherToTenantWeatherState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OutdoorCheckedAtUtc",
                table: "tenantWeatherState",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OutdoorHumidityPercent",
                table: "tenantWeatherState",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OutdoorTemperatureC",
                table: "tenantWeatherState",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OutdoorWindSpeedMetersPerSecond",
                table: "tenantWeatherState",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OutdoorCheckedAtUtc",
                table: "tenantWeatherState");

            migrationBuilder.DropColumn(
                name: "OutdoorHumidityPercent",
                table: "tenantWeatherState");

            migrationBuilder.DropColumn(
                name: "OutdoorTemperatureC",
                table: "tenantWeatherState");

            migrationBuilder.DropColumn(
                name: "OutdoorWindSpeedMetersPerSecond",
                table: "tenantWeatherState");
        }
    }
}
