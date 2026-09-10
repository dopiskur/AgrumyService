using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "device",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocationSource",
                table: "device",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "device",
                type: "double",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "device");

            migrationBuilder.DropColumn(
                name: "LocationSource",
                table: "device");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "device");
        }
    }
}
