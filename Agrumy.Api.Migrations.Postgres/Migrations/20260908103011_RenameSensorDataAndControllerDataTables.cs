using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RenameSensorDataAndControllerDataTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_controllerData_device_DeviceID",
                table: "controllerData");

            migrationBuilder.DropForeignKey(
                name: "FK_sensorData_deviceFarmUnitZone_DeviceFarmUnitZoneID",
                table: "sensorData");

            migrationBuilder.DropForeignKey(
                name: "FK_sensorData_deviceFarmUnit_DeviceFarmUnitID",
                table: "sensorData");

            migrationBuilder.DropForeignKey(
                name: "FK_sensorData_device_DeviceID",
                table: "sensorData");

            migrationBuilder.DropPrimaryKey(
                name: "PK_sensorData",
                table: "sensorData");

            migrationBuilder.DropPrimaryKey(
                name: "PK_controllerData",
                table: "controllerData");

            migrationBuilder.RenameTable(
                name: "sensorData",
                newName: "dataSensor");

            migrationBuilder.RenameTable(
                name: "controllerData",
                newName: "dataController");

            migrationBuilder.RenameIndex(
                name: "ix_sensorData_deviceFarmUnitZone_date",
                table: "dataSensor",
                newName: "ix_dataSensor_deviceFarmUnitZone_date");

            migrationBuilder.RenameIndex(
                name: "IX_sensorData_DeviceFarmUnitID",
                table: "dataSensor",
                newName: "IX_dataSensor_DeviceFarmUnitID");

            migrationBuilder.RenameIndex(
                name: "ix_sensorData_device_tenant_date",
                table: "dataSensor",
                newName: "ix_dataSensor_device_tenant_date");

            migrationBuilder.RenameIndex(
                name: "ux_controllerData_device_relayFunction",
                table: "dataController",
                newName: "ux_dataController_device_relayFunction");

            migrationBuilder.AddPrimaryKey(
                name: "PK_dataSensor",
                table: "dataSensor",
                column: "IDSensorData");

            migrationBuilder.AddPrimaryKey(
                name: "PK_dataController",
                table: "dataController",
                column: "IDControllerData");

            migrationBuilder.AddForeignKey(
                name: "FK_dataController_device_DeviceID",
                table: "dataController",
                column: "DeviceID",
                principalTable: "device",
                principalColumn: "IDDevice");

            migrationBuilder.AddForeignKey(
                name: "FK_dataSensor_deviceFarmUnitZone_DeviceFarmUnitZoneID",
                table: "dataSensor",
                column: "DeviceFarmUnitZoneID",
                principalTable: "deviceFarmUnitZone",
                principalColumn: "IDDeviceFarmUnitZone");

            migrationBuilder.AddForeignKey(
                name: "FK_dataSensor_deviceFarmUnit_DeviceFarmUnitID",
                table: "dataSensor",
                column: "DeviceFarmUnitID",
                principalTable: "deviceFarmUnit",
                principalColumn: "IDDeviceFarmUnit");

            migrationBuilder.AddForeignKey(
                name: "FK_dataSensor_device_DeviceID",
                table: "dataSensor",
                column: "DeviceID",
                principalTable: "device",
                principalColumn: "IDDevice");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dataController_device_DeviceID",
                table: "dataController");

            migrationBuilder.DropForeignKey(
                name: "FK_dataSensor_deviceFarmUnitZone_DeviceFarmUnitZoneID",
                table: "dataSensor");

            migrationBuilder.DropForeignKey(
                name: "FK_dataSensor_deviceFarmUnit_DeviceFarmUnitID",
                table: "dataSensor");

            migrationBuilder.DropForeignKey(
                name: "FK_dataSensor_device_DeviceID",
                table: "dataSensor");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dataSensor",
                table: "dataSensor");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dataController",
                table: "dataController");

            migrationBuilder.RenameTable(
                name: "dataSensor",
                newName: "sensorData");

            migrationBuilder.RenameTable(
                name: "dataController",
                newName: "controllerData");

            migrationBuilder.RenameIndex(
                name: "ix_dataSensor_deviceFarmUnitZone_date",
                table: "sensorData",
                newName: "ix_sensorData_deviceFarmUnitZone_date");

            migrationBuilder.RenameIndex(
                name: "IX_dataSensor_DeviceFarmUnitID",
                table: "sensorData",
                newName: "IX_sensorData_DeviceFarmUnitID");

            migrationBuilder.RenameIndex(
                name: "ix_dataSensor_device_tenant_date",
                table: "sensorData",
                newName: "ix_sensorData_device_tenant_date");

            migrationBuilder.RenameIndex(
                name: "ux_dataController_device_relayFunction",
                table: "controllerData",
                newName: "ux_controllerData_device_relayFunction");

            migrationBuilder.AddPrimaryKey(
                name: "PK_sensorData",
                table: "sensorData",
                column: "IDSensorData");

            migrationBuilder.AddPrimaryKey(
                name: "PK_controllerData",
                table: "controllerData",
                column: "IDControllerData");

            migrationBuilder.AddForeignKey(
                name: "FK_controllerData_device_DeviceID",
                table: "controllerData",
                column: "DeviceID",
                principalTable: "device",
                principalColumn: "IDDevice");

            migrationBuilder.AddForeignKey(
                name: "FK_sensorData_deviceFarmUnitZone_DeviceFarmUnitZoneID",
                table: "sensorData",
                column: "DeviceFarmUnitZoneID",
                principalTable: "deviceFarmUnitZone",
                principalColumn: "IDDeviceFarmUnitZone");

            migrationBuilder.AddForeignKey(
                name: "FK_sensorData_deviceFarmUnit_DeviceFarmUnitID",
                table: "sensorData",
                column: "DeviceFarmUnitID",
                principalTable: "deviceFarmUnit",
                principalColumn: "IDDeviceFarmUnit");

            migrationBuilder.AddForeignKey(
                name: "FK_sensorData_device_DeviceID",
                table: "sensorData",
                column: "DeviceID",
                principalTable: "device",
                principalColumn: "IDDevice");
        }
    }
}
