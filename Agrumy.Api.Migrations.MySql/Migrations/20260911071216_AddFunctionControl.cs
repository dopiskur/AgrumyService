using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
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
                    IDDeviceConfigController = table.Column<int>(type: "int", nullable: false),
                    RelayFunction = table.Column<int>(type: "int", nullable: false),
                    ControlMode = table.Column<int>(type: "int", nullable: false),
                    PidSetpointMetric = table.Column<int>(type: "int", nullable: true),
                    PidSetpoint = table.Column<double>(type: "double", nullable: true),
                    PidKp = table.Column<double>(type: "double", nullable: true),
                    PidKi = table.Column<double>(type: "double", nullable: true),
                    PidKd = table.Column<double>(type: "double", nullable: true),
                    PidSampleIntervalSeconds = table.Column<double>(type: "double", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceConfigControllerFunctionControl", x => new { x.IDDeviceConfigController, x.RelayFunction });
                    table.ForeignKey(
                        name: "FK_deviceConfigControllerFunctionControl_deviceConfigController~",
                        column: x => x.IDDeviceConfigController,
                        principalTable: "deviceConfigController",
                        principalColumn: "IDDeviceConfigController",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_deviceConfigControllerFunctionControl_deviceTypeRelay_RelayF~",
                        column: x => x.RelayFunction,
                        principalTable: "deviceTypeRelay",
                        principalColumn: "IDDeviceTypeRelay");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
