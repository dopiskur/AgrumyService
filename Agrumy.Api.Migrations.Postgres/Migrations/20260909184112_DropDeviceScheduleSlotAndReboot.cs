using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
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
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "deviceScheduleSlot",
                columns: table => new
                {
                    IDDeviceScheduleSlot = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DaysOfWeek = table.Column<int>(type: "integer", nullable: false),
                    DeviceConfigControllerID = table.Column<int>(type: "integer", nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    RelayFunction = table.Column<int>(type: "integer", nullable: false),
                    Start = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceScheduleSlot", x => x.IDDeviceScheduleSlot);
                    table.ForeignKey(
                        name: "FK_deviceScheduleSlot_deviceConfigController_DeviceConfigContr~",
                        column: x => x.DeviceConfigControllerID,
                        principalTable: "deviceConfigController",
                        principalColumn: "IDDeviceConfigController");
                });

            migrationBuilder.CreateIndex(
                name: "IX_deviceScheduleSlot_DeviceConfigControllerID",
                table: "deviceScheduleSlot",
                column: "DeviceConfigControllerID");
        }
    }
}
