using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddFunctionControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deviceConfigControllerFunctionControl",
                columns: table => new
                {
                    IDDeviceConfigController = table.Column<int>(type: "integer", nullable: false),
                    RelayFunction = table.Column<int>(type: "integer", nullable: false),
                    ControlMode = table.Column<int>(type: "integer", nullable: false),
                    PidSetpointMetric = table.Column<int>(type: "integer", nullable: true),
                    PidSetpoint = table.Column<double>(type: "double precision", nullable: true),
                    PidKp = table.Column<double>(type: "double precision", nullable: true),
                    PidKi = table.Column<double>(type: "double precision", nullable: true),
                    PidKd = table.Column<double>(type: "double precision", nullable: true),
                    PidSampleIntervalSeconds = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceConfigControllerFunctionControl", x => new { x.IDDeviceConfigController, x.RelayFunction });
                    table.ForeignKey(
                        name: "FK_deviceConfigControllerFunctionControl_deviceConfigControlle~",
                        column: x => x.IDDeviceConfigController,
                        principalTable: "deviceConfigController",
                        principalColumn: "IDDeviceConfigController",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_deviceConfigControllerFunctionControl_deviceTypeRelay_Relay~",
                        column: x => x.RelayFunction,
                        principalTable: "deviceTypeRelay",
                        principalColumn: "IDDeviceTypeRelay");
                });

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigControllerFunctionControl_RelayFunction",
                table: "deviceConfigControllerFunctionControl",
                column: "RelayFunction");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deviceConfigControllerFunctionControl");
        }
    }
}
