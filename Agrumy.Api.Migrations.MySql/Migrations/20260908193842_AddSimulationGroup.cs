using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddSimulationGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IDSimulationGroup",
                table: "simulationSessionDevice",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "simulationGroup",
                columns: table => new
                {
                    IDSimulationGroup = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IDSimulationSession = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    ScopeID = table.Column<int>(type: "int", nullable: false),
                    Temperature = table.Column<double>(type: "double", nullable: true),
                    SoilTemperature = table.Column<double>(type: "double", nullable: true),
                    Humidity = table.Column<double>(type: "double", nullable: true),
                    Battery = table.Column<int>(type: "int", nullable: true),
                    Moisture = table.Column<int>(type: "int", nullable: true),
                    Light = table.Column<int>(type: "int", nullable: true),
                    Co2 = table.Column<int>(type: "int", nullable: true),
                    Tvoc = table.Column<int>(type: "int", nullable: true),
                    Barometer = table.Column<double>(type: "double", nullable: true),
                    LiquidPH = table.Column<double>(type: "double", nullable: true),
                    RainLevel = table.Column<int>(type: "int", nullable: true),
                    WaterLevel = table.Column<int>(type: "int", nullable: true),
                    Wind = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulationGroup", x => x.IDSimulationGroup);
                    table.ForeignKey(
                        name: "FK_simulationGroup_simulationSession_IDSimulationSession",
                        column: x => x.IDSimulationSession,
                        principalTable: "simulationSession",
                        principalColumn: "IDSimulationSession",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_simulationSessionDevice_IDSimulationGroup",
                table: "simulationSessionDevice",
                column: "IDSimulationGroup");

            migrationBuilder.CreateIndex(
                name: "ix_simulationGroup_session",
                table: "simulationGroup",
                column: "IDSimulationSession");

            migrationBuilder.AddForeignKey(
                name: "FK_simulationSessionDevice_simulationGroup_IDSimulationGroup",
                table: "simulationSessionDevice",
                column: "IDSimulationGroup",
                principalTable: "simulationGroup",
                principalColumn: "IDSimulationGroup",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_simulationSessionDevice_simulationGroup_IDSimulationGroup",
                table: "simulationSessionDevice");

            migrationBuilder.DropTable(
                name: "simulationGroup");

            migrationBuilder.DropIndex(
                name: "IX_simulationSessionDevice_IDSimulationGroup",
                table: "simulationSessionDevice");

            migrationBuilder.DropColumn(
                name: "IDSimulationGroup",
                table: "simulationSessionDevice");
        }
    }
}
