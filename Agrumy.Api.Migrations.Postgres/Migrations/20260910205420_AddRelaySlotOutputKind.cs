using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddRelaySlotOutputKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LatchingPulseMs",
                table: "deviceConfigControllerRelay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinOffSeconds",
                table: "deviceConfigControllerRelay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinOnSeconds",
                table: "deviceConfigControllerRelay",
                type: "integer",
                nullable: true);

            // defaultValue: 1 (Relay), not 0 - every row that predates this column is a real relay slot already
            // behaving as Relay-kind; 0 isn't a valid OutputKind at all, and firmware's dispatch switch treats an
            // unrecognized outputKind as a silent no-op, which would have gone straight to disabling every
            // existing relay slot on every real device the moment this migration ran.
            migrationBuilder.AddColumn<int>(
                name: "OutputKind",
                table: "deviceConfigControllerRelay",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PairSlot",
                table: "deviceConfigControllerRelay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PwmFrequencyHz",
                table: "deviceConfigControllerRelay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RateLimitPercentPerSecond",
                table: "deviceConfigControllerRelay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServoMaxPulseUs",
                table: "deviceConfigControllerRelay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServoMinPulseUs",
                table: "deviceConfigControllerRelay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServoSafePositionPercent",
                table: "deviceConfigControllerRelay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeProportioningPeriodSeconds",
                table: "deviceConfigControllerRelay",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TravelSeconds",
                table: "deviceConfigControllerRelay",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatchingPulseMs",
                table: "deviceConfigControllerRelay");

            migrationBuilder.DropColumn(
                name: "MinOffSeconds",
                table: "deviceConfigControllerRelay");

            migrationBuilder.DropColumn(
                name: "MinOnSeconds",
                table: "deviceConfigControllerRelay");

            migrationBuilder.DropColumn(
                name: "OutputKind",
                table: "deviceConfigControllerRelay");

            migrationBuilder.DropColumn(
                name: "PairSlot",
                table: "deviceConfigControllerRelay");

            migrationBuilder.DropColumn(
                name: "PwmFrequencyHz",
                table: "deviceConfigControllerRelay");

            migrationBuilder.DropColumn(
                name: "RateLimitPercentPerSecond",
                table: "deviceConfigControllerRelay");

            migrationBuilder.DropColumn(
                name: "ServoMaxPulseUs",
                table: "deviceConfigControllerRelay");

            migrationBuilder.DropColumn(
                name: "ServoMinPulseUs",
                table: "deviceConfigControllerRelay");

            migrationBuilder.DropColumn(
                name: "ServoSafePositionPercent",
                table: "deviceConfigControllerRelay");

            migrationBuilder.DropColumn(
                name: "TimeProportioningPeriodSeconds",
                table: "deviceConfigControllerRelay");

            migrationBuilder.DropColumn(
                name: "TravelSeconds",
                table: "deviceConfigControllerRelay");
        }
    }
}
