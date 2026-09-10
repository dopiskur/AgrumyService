using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    // Hand-edited after scaffolding - same reason as the MySql migration of the same name: the raw
    // scaffold was a DropTable+CreateTable (data loss on every pending/historical deviceCommand row),
    // rewritten as Rename*/AddColumn so existing rows survive with their IDs/values intact.
    public partial class RenameDeviceCommandToDeviceOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_deviceCommand_device_DeviceID",
                table: "deviceCommand");

            migrationBuilder.DropPrimaryKey(
                name: "PK_deviceCommand",
                table: "deviceCommand");

            migrationBuilder.RenameTable(
                name: "deviceCommand",
                newName: "deviceOutbox");

            migrationBuilder.RenameColumn(
                name: "IDDeviceCommand",
                table: "deviceOutbox",
                newName: "IDDeviceOutbox");

            migrationBuilder.RenameColumn(
                name: "ActionType",
                table: "deviceOutbox",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "IssuedAt",
                table: "deviceOutbox",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_deviceCommand_device_status",
                table: "deviceOutbox",
                newName: "ix_deviceOutbox_device_status");

            migrationBuilder.RenameIndex(
                name: "ux_deviceCommand_device_activekey",
                table: "deviceOutbox",
                newName: "ux_deviceOutbox_device_activekey");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "deviceOutbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_deviceOutbox",
                table: "deviceOutbox",
                column: "IDDeviceOutbox");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceOutbox_device_DeviceID",
                table: "deviceOutbox",
                column: "DeviceID",
                principalTable: "device",
                principalColumn: "IDDevice");

            migrationBuilder.CreateIndex(
                name: "ix_deviceOutbox_pending_unpublished",
                table: "deviceOutbox",
                columns: new[] { "Status", "PublishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_deviceOutbox_pending_unpublished",
                table: "deviceOutbox");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceOutbox_device_DeviceID",
                table: "deviceOutbox");

            migrationBuilder.DropPrimaryKey(
                name: "PK_deviceOutbox",
                table: "deviceOutbox");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "deviceOutbox");

            migrationBuilder.RenameIndex(
                name: "ux_deviceOutbox_device_activekey",
                table: "deviceOutbox",
                newName: "ux_deviceCommand_device_activekey");

            migrationBuilder.RenameIndex(
                name: "ix_deviceOutbox_device_status",
                table: "deviceOutbox",
                newName: "ix_deviceCommand_device_status");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "deviceOutbox",
                newName: "IssuedAt");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "deviceOutbox",
                newName: "ActionType");

            migrationBuilder.RenameColumn(
                name: "IDDeviceOutbox",
                table: "deviceOutbox",
                newName: "IDDeviceCommand");

            migrationBuilder.RenameTable(
                name: "deviceOutbox",
                newName: "deviceCommand");

            migrationBuilder.AddPrimaryKey(
                name: "PK_deviceCommand",
                table: "deviceCommand",
                column: "IDDeviceCommand");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceCommand_device_DeviceID",
                table: "deviceCommand",
                column: "DeviceID",
                principalTable: "device",
                principalColumn: "IDDevice");
        }
    }
}
