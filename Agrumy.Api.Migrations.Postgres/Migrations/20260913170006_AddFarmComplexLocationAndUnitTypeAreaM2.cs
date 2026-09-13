using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmComplexLocationAndUnitTypeAreaM2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AreaHectares",
                table: "farmGreenhouseUnit",
                newName: "AreaSquareMeters");

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "farmGreenhouseUnit",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "farmGreenhouseUnit",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitType",
                table: "farmGreenhouseUnit",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "farm",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "farm",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "farmGreenhouseUnit");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "farmGreenhouseUnit");

            migrationBuilder.DropColumn(
                name: "UnitType",
                table: "farmGreenhouseUnit");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "farm");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "farm");

            migrationBuilder.RenameColumn(
                name: "AreaSquareMeters",
                table: "farmGreenhouseUnit",
                newName: "AreaHectares");
        }
    }
}
