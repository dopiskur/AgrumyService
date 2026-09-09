using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class DropDeviceScheduleSlotAndReboot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deviceScheduleSlot");

            migrationBuilder.DropColumn(
                name: "Reboot",
                table: "device");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Reboot",
                table: "device",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "deviceScheduleSlot",
                columns: table => new
                {
                    IDDeviceScheduleSlot = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DaysOfWeek = table.Column<int>(type: "int", nullable: false),
                    DeviceConfigControllerID = table.Column<int>(type: "int", nullable: false),
                    Duration = table.Column<int>(type: "int", nullable: false),
                    RelayFunction = table.Column<int>(type: "int", nullable: false),
                    Start = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceScheduleSlot", x => x.IDDeviceScheduleSlot);
                    table.ForeignKey(
                        name: "FK_deviceScheduleSlot_deviceConfigController_DeviceConfigContro~",
                        column: x => x.DeviceConfigControllerID,
                        principalTable: "deviceConfigController",
                        principalColumn: "IDDeviceConfigController");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_deviceScheduleSlot_DeviceConfigControllerID",
                table: "deviceScheduleSlot",
                column: "DeviceConfigControllerID");
        }
    }
}
