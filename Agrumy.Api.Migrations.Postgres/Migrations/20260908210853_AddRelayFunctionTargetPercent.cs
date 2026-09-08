using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddRelayFunctionTargetPercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TargetPercent",
                table: "deviceFarmUnitZoneRule",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Percent",
                table: "dataControllerExperiment",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Percent",
                table: "dataController",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetPercent",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropColumn(
                name: "Percent",
                table: "dataControllerExperiment");

            migrationBuilder.DropColumn(
                name: "Percent",
                table: "dataController");
        }
    }
}
