using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddPowerRailEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PowerRailPrimaryEnabled",
                table: "device",
                type: "tinyint(1)",
                nullable: true,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PowerRailSecondaryEnabled",
                table: "device",
                type: "tinyint(1)",
                nullable: true,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PowerRailPrimaryEnabled",
                table: "device");

            migrationBuilder.DropColumn(
                name: "PowerRailSecondaryEnabled",
                table: "device");
        }
    }
}
