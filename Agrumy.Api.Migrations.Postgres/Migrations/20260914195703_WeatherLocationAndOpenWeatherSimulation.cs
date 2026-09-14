using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class WeatherLocationAndOpenWeatherSimulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenantWeatherState");

            migrationBuilder.AddColumn<string>(
                name: "WeatherApiKey",
                table: "serverConfig",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SimulatedOutdoorHumidity",
                table: "deviceSimulation",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SimulatedOutdoorPressure",
                table: "deviceSimulation",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SimulatedOutdoorTemperature",
                table: "deviceSimulation",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SimulatedOutdoorWind",
                table: "deviceSimulation",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "weatherLocationState",
                columns: table => new
                {
                    IDWeatherLocationState = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    WeatherRainPredicted = table.Column<bool>(type: "boolean", nullable: false),
                    WeatherCheckedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FrostPredicted = table.Column<bool>(type: "boolean", nullable: false),
                    FrostPredictedHoursAhead = table.Column<int>(type: "integer", nullable: true),
                    FrostCheckedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OutdoorTemperatureC = table.Column<double>(type: "double precision", nullable: true),
                    OutdoorHumidityPercent = table.Column<double>(type: "double precision", nullable: true),
                    OutdoorWindSpeedMetersPerSecond = table.Column<double>(type: "double precision", nullable: true),
                    OutdoorPressureHpa = table.Column<double>(type: "double precision", nullable: true),
                    OutdoorCheckedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weatherLocationState", x => x.IDWeatherLocationState);
                    table.ForeignKey(
                        name: "FK_weatherLocationState_tenant_TenantID",
                        column: x => x.TenantID,
                        principalTable: "tenant",
                        principalColumn: "IDTenant",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_weatherLocationState_TenantID_Latitude_Longitude",
                table: "weatherLocationState",
                columns: new[] { "TenantID", "Latitude", "Longitude" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "weatherLocationState");

            migrationBuilder.DropColumn(
                name: "WeatherApiKey",
                table: "serverConfig");

            migrationBuilder.DropColumn(
                name: "SimulatedOutdoorHumidity",
                table: "deviceSimulation");

            migrationBuilder.DropColumn(
                name: "SimulatedOutdoorPressure",
                table: "deviceSimulation");

            migrationBuilder.DropColumn(
                name: "SimulatedOutdoorTemperature",
                table: "deviceSimulation");

            migrationBuilder.DropColumn(
                name: "SimulatedOutdoorWind",
                table: "deviceSimulation");

            migrationBuilder.CreateTable(
                name: "tenantWeatherState",
                columns: table => new
                {
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    FrostCheckedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FrostPredicted = table.Column<bool>(type: "boolean", nullable: false),
                    FrostPredictedHoursAhead = table.Column<int>(type: "integer", nullable: true),
                    OutdoorCheckedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OutdoorHumidityPercent = table.Column<double>(type: "double precision", nullable: true),
                    OutdoorTemperatureC = table.Column<double>(type: "double precision", nullable: true),
                    OutdoorWindSpeedMetersPerSecond = table.Column<double>(type: "double precision", nullable: true),
                    WeatherCheckedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WeatherRainPredicted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenantWeatherState", x => x.TenantID);
                    table.ForeignKey(
                        name: "FK_tenantWeatherState_tenant_TenantID",
                        column: x => x.TenantID,
                        principalTable: "tenant",
                        principalColumn: "IDTenant",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
