using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
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
                    TenantID = table.Column<int>(type: "int", nullable: false),
                    WeatherRainPredicted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    WeatherCheckedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    FrostPredicted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FrostPredictedHoursAhead = table.Column<int>(type: "int", nullable: true),
                    FrostCheckedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenantWeatherState");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FrostCheckedAtUtc",
                table: "serverConfig",
                type: "datetime(6)",
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

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WeatherCheckedAtUtc",
                table: "serverConfig",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WeatherRainPredicted",
                table: "serverConfig",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }
    }
}
