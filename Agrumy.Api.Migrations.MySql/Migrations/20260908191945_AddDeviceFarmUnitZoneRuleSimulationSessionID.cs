using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceFarmUnitZoneRuleSimulationSessionID : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SimulationSessionID",
                table: "deviceFarmUnitZoneRule",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_deviceFarmUnitZoneRule_simulationSession",
                table: "deviceFarmUnitZoneRule",
                column: "SimulationSessionID");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_simulationSession_SimulationSessionID",
                table: "deviceFarmUnitZoneRule",
                column: "SimulationSessionID",
                principalTable: "simulationSession",
                principalColumn: "IDSimulationSession",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_simulationSession_SimulationSessionID",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropIndex(
                name: "ix_deviceFarmUnitZoneRule_simulationSession",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropColumn(
                name: "SimulationSessionID",
                table: "deviceFarmUnitZoneRule");
        }
    }
}
