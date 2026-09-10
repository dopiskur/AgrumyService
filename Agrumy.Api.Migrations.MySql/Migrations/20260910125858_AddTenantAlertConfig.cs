using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAlertConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BatteryLowHysteresis",
                table: "tenant",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BatteryLowThreshold",
                table: "tenant",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EventDedupeMinutes",
                table: "tenant",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProblemEventAlertsEnabled",
                table: "tenant",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProblemEventExpiryHours",
                table: "tenant",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TankRefillHysteresis",
                table: "tenant",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TankRefillThreshold",
                table: "tenant",
                type: "double",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatteryLowHysteresis",
                table: "tenant");

            migrationBuilder.DropColumn(
                name: "BatteryLowThreshold",
                table: "tenant");

            migrationBuilder.DropColumn(
                name: "EventDedupeMinutes",
                table: "tenant");

            migrationBuilder.DropColumn(
                name: "ProblemEventAlertsEnabled",
                table: "tenant");

            migrationBuilder.DropColumn(
                name: "ProblemEventExpiryHours",
                table: "tenant");

            migrationBuilder.DropColumn(
                name: "TankRefillHysteresis",
                table: "tenant");

            migrationBuilder.DropColumn(
                name: "TankRefillThreshold",
                table: "tenant");
        }
    }
}
