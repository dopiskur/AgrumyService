using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class OpenWeatherFeedTogglesNotValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<bool>(
                name: "SimulateOutdoorHumidity",
                table: "deviceSimulation",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SimulateOutdoorPressure",
                table: "deviceSimulation",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SimulateOutdoorTemperature",
                table: "deviceSimulation",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SimulateOutdoorWind",
                table: "deviceSimulation",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SimulateOutdoorHumidity",
                table: "deviceSimulation");

            migrationBuilder.DropColumn(
                name: "SimulateOutdoorPressure",
                table: "deviceSimulation");

            migrationBuilder.DropColumn(
                name: "SimulateOutdoorTemperature",
                table: "deviceSimulation");

            migrationBuilder.DropColumn(
                name: "SimulateOutdoorWind",
                table: "deviceSimulation");

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
        }
    }
}
