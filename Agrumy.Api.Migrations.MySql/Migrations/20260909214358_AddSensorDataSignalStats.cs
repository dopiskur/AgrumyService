using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddSensorDataSignalStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LoRaRssiDbm",
                table: "dataSensor",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoRaSnrDb",
                table: "dataSensor",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WifiRssiDbm",
                table: "dataSensor",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoRaRssiDbm",
                table: "dataSensor");

            migrationBuilder.DropColumn(
                name: "LoRaSnrDb",
                table: "dataSensor");

            migrationBuilder.DropColumn(
                name: "WifiRssiDbm",
                table: "dataSensor");
        }
    }
}
