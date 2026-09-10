using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSensorDataReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sensorDataReport");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sensorDataReport",
                columns: table => new
                {
                    IDSensorDataReport = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DateGenerated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    deviceID = table.Column<int>(type: "integer", nullable: true),
                    ReportName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    sensorData = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensorDataReport", x => x.IDSensorDataReport);
                    table.ForeignKey(
                        name: "FK_sensorDataReport_device_deviceID",
                        column: x => x.deviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                });

            migrationBuilder.CreateIndex(
                name: "IX_sensorDataReport_deviceID",
                table: "sensorDataReport",
                column: "deviceID");
        }
    }
}
