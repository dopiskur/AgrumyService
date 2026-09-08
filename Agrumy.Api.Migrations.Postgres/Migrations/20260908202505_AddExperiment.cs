using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
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
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "experiment",
                columns: table => new
                {
                    IDExperiment = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ScopeID = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StoppedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiment", x => x.IDExperiment);
                });

            migrationBuilder.CreateTable(
                name: "dataControllerExperiment",
                columns: table => new
                {
                    IDControllerDataExperiment = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IDExperiment = table.Column<int>(type: "integer", nullable: false),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    DeviceID = table.Column<int>(type: "integer", nullable: false),
                    RelayFunction = table.Column<int>(type: "integer", nullable: false),
                    IsOn = table.Column<bool>(type: "boolean", nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "dataSensorExperiment",
                columns: table => new
                {
                    IDSensorDataExperiment = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IDExperiment = table.Column<int>(type: "integer", nullable: false),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    DeviceID = table.Column<int>(type: "integer", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: true),
                    SoilTemperature = table.Column<double>(type: "double precision", nullable: true),
                    Humidity = table.Column<double>(type: "double precision", nullable: true),
                    Moisture = table.Column<int>(type: "integer", nullable: true),
                    Light = table.Column<int>(type: "integer", nullable: true),
                    Co2 = table.Column<int>(type: "integer", nullable: true),
                    Tvoc = table.Column<int>(type: "integer", nullable: true),
                    Barometer = table.Column<double>(type: "double precision", nullable: true),
                    LiquidPH = table.Column<double>(type: "double precision", nullable: true),
                    RainLevel = table.Column<int>(type: "integer", nullable: true),
                    WaterLevel = table.Column<int>(type: "integer", nullable: true),
                    Wind = table.Column<int>(type: "integer", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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
                });

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
