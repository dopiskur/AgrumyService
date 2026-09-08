using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddExperiment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExperimentID",
                table: "deviceFarmUnitZoneRule",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "experiment",
                columns: table => new
                {
                    IDExperiment = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TenantID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    ScopeID = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    StoppedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiment", x => x.IDExperiment);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "dataControllerExperiment",
                columns: table => new
                {
                    IDControllerDataExperiment = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IDExperiment = table.Column<int>(type: "int", nullable: false),
                    TenantID = table.Column<int>(type: "int", nullable: false),
                    DeviceID = table.Column<int>(type: "int", nullable: false),
                    RelayFunction = table.Column<int>(type: "int", nullable: false),
                    IsOn = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dataControllerExperiment", x => x.IDControllerDataExperiment);
                    table.ForeignKey(
                        name: "FK_dataControllerExperiment_device_DeviceID",
                        column: x => x.DeviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                    table.ForeignKey(
                        name: "FK_dataControllerExperiment_experiment_IDExperiment",
                        column: x => x.IDExperiment,
                        principalTable: "experiment",
                        principalColumn: "IDExperiment");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "dataSensorExperiment",
                columns: table => new
                {
                    IDSensorDataExperiment = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IDExperiment = table.Column<int>(type: "int", nullable: false),
                    TenantID = table.Column<int>(type: "int", nullable: false),
                    DeviceID = table.Column<int>(type: "int", nullable: false),
                    Temperature = table.Column<double>(type: "double", nullable: true),
                    SoilTemperature = table.Column<double>(type: "double", nullable: true),
                    Humidity = table.Column<double>(type: "double", nullable: true),
                    Moisture = table.Column<int>(type: "int", nullable: true),
                    Light = table.Column<int>(type: "int", nullable: true),
                    Co2 = table.Column<int>(type: "int", nullable: true),
                    Tvoc = table.Column<int>(type: "int", nullable: true),
                    Barometer = table.Column<double>(type: "double", nullable: true),
                    LiquidPH = table.Column<double>(type: "double", nullable: true),
                    RainLevel = table.Column<int>(type: "int", nullable: true),
                    WaterLevel = table.Column<int>(type: "int", nullable: true),
                    Wind = table.Column<int>(type: "int", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dataSensorExperiment", x => x.IDSensorDataExperiment);
                    table.ForeignKey(
                        name: "FK_dataSensorExperiment_device_DeviceID",
                        column: x => x.DeviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                    table.ForeignKey(
                        name: "FK_dataSensorExperiment_experiment_IDExperiment",
                        column: x => x.IDExperiment,
                        principalTable: "experiment",
                        principalColumn: "IDExperiment");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_deviceFarmUnitZoneRule_experiment",
                table: "deviceFarmUnitZoneRule",
                column: "ExperimentID");

            migrationBuilder.CreateIndex(
                name: "IX_dataControllerExperiment_DeviceID",
                table: "dataControllerExperiment",
                column: "DeviceID");

            migrationBuilder.CreateIndex(
                name: "ix_dataControllerExperiment_experiment_date",
                table: "dataControllerExperiment",
                columns: new[] { "IDExperiment", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_dataSensorExperiment_DeviceID",
                table: "dataSensorExperiment",
                column: "DeviceID");

            migrationBuilder.CreateIndex(
                name: "ix_dataSensorExperiment_experiment_date",
                table: "dataSensorExperiment",
                columns: new[] { "IDExperiment", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "ix_experiment_tenant_scope",
                table: "experiment",
                columns: new[] { "TenantID", "Scope", "ScopeID" });

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_experiment_ExperimentID",
                table: "deviceFarmUnitZoneRule",
                column: "ExperimentID",
                principalTable: "experiment",
                principalColumn: "IDExperiment",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_experiment_ExperimentID",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropTable(
                name: "dataControllerExperiment");

            migrationBuilder.DropTable(
                name: "dataSensorExperiment");

            migrationBuilder.DropTable(
                name: "experiment");

            migrationBuilder.DropIndex(
                name: "ix_deviceFarmUnitZoneRule_experiment",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropColumn(
                name: "ExperimentID",
                table: "deviceFarmUnitZoneRule");
        }
    }
}
