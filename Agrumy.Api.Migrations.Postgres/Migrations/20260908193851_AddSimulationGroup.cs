using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
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
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "simulationGroup",
                columns: table => new
                {
                    IDSimulationGroup = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IDSimulationSession = table.Column<int>(type: "integer", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ScopeID = table.Column<int>(type: "integer", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: true),
                    SoilTemperature = table.Column<double>(type: "double precision", nullable: true),
                    Humidity = table.Column<double>(type: "double precision", nullable: true),
                    Battery = table.Column<int>(type: "integer", nullable: true),
                    Moisture = table.Column<int>(type: "integer", nullable: true),
                    Light = table.Column<int>(type: "integer", nullable: true),
                    Co2 = table.Column<int>(type: "integer", nullable: true),
                    Tvoc = table.Column<int>(type: "integer", nullable: true),
                    Barometer = table.Column<double>(type: "double precision", nullable: true),
                    LiquidPH = table.Column<double>(type: "double precision", nullable: true),
                    RainLevel = table.Column<int>(type: "integer", nullable: true),
                    WaterLevel = table.Column<int>(type: "integer", nullable: true),
                    Wind = table.Column<int>(type: "integer", nullable: true)
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
                });

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
