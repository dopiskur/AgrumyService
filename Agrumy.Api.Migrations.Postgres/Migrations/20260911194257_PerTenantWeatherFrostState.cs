using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class PerTenantWeatherFrostState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FrostCheckedAtUtc",
                table: "serverConfig");

            migrationBuilder.DropColumn(
                name: "FrostPredicted",
                table: "serverConfig");

            migrationBuilder.DropColumn(
                name: "FrostPredictedHoursAhead",
                table: "serverConfig");

            migrationBuilder.DropColumn(
                name: "WeatherCheckedAtUtc",
                table: "serverConfig");

            migrationBuilder.DropColumn(
                name: "WeatherRainPredicted",
                table: "serverConfig");

            migrationBuilder.CreateTable(
                name: "tenantWeatherState",
                columns: table => new
                {
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    WeatherRainPredicted = table.Column<bool>(type: "boolean", nullable: false),
                    WeatherCheckedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FrostPredicted = table.Column<bool>(type: "boolean", nullable: false),
                    FrostPredictedHoursAhead = table.Column<int>(type: "integer", nullable: true),
                    FrostCheckedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenantWeatherState");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FrostCheckedAtUtc",
                table: "serverConfig",
                type: "timestamp with time zone",
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

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WeatherCheckedAtUtc",
                table: "serverConfig",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WeatherRainPredicted",
                table: "serverConfig",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
