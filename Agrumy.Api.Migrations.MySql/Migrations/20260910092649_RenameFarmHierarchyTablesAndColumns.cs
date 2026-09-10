using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class RenameFarmHierarchyTablesAndColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dataSensor_deviceFarmUnitZone_DeviceFarmUnitZoneID",
                table: "dataSensor");

            migrationBuilder.DropForeignKey(
                name: "FK_dataSensor_deviceFarmUnit_DeviceFarmUnitID",
                table: "dataSensor");

            migrationBuilder.DropForeignKey(
                name: "FK_device_deviceFarmUnit_DeviceFarmUnitID",
                table: "device");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnit_deviceFarm_DeviceFarmID",
                table: "deviceFarmUnit");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZone_deviceFarmUnit_DeviceFarmUnitID",
                table: "deviceFarmUnitZone");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_deviceFarmUnitZone_DeviceFarmUnitZone~",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_deviceFarmUnit_DeviceFarmUnitID",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_deviceFarm_DeviceFarmID",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropForeignKey(
                name: "FK_ruleNotificationState_deviceFarmUnitZone_DeviceFarmUnitZoneID",
                table: "ruleNotificationState");

            migrationBuilder.DropPrimaryKey(
                name: "PK_deviceFarmUnitZone",
                table: "deviceFarmUnitZone");

            migrationBuilder.DropPrimaryKey(
                name: "PK_deviceFarmUnit",
                table: "deviceFarmUnit");

            // MySQL refuses DROP PRIMARY KEY on an AUTO_INCREMENT column outright - strip it first, restored after AddPrimaryKey below.
            migrationBuilder.Sql("ALTER TABLE deviceFarm MODIFY COLUMN IDDeviceFarm int NOT NULL;");

            migrationBuilder.DropPrimaryKey(
                name: "PK_deviceFarm",
                table: "deviceFarm");

            migrationBuilder.RenameTable(
                name: "deviceFarmUnitZone",
                newName: "farmGreenhouseUnitZone");

            migrationBuilder.RenameTable(
                name: "deviceFarmUnit",
                newName: "farmGreenhouseUnit");

            migrationBuilder.RenameTable(
                name: "deviceFarm",
                newName: "farm");

            migrationBuilder.RenameColumn(
                name: "DeviceFarmUnitZoneID",
                table: "device",
                newName: "FarmGreenhouseUnitZoneID");

            migrationBuilder.RenameColumn(
                name: "DeviceFarmUnitID",
                table: "device",
                newName: "FarmGreenhouseUnitID");

            migrationBuilder.RenameIndex(
                name: "IX_device_DeviceFarmUnitID",
                table: "device",
                newName: "IX_device_FarmGreenhouseUnitID");

            migrationBuilder.RenameColumn(
                name: "DeviceFarmUnitZoneID",
                table: "dataSensor",
                newName: "FarmGreenhouseUnitZoneID");

            migrationBuilder.RenameColumn(
                name: "DeviceFarmUnitID",
                table: "dataSensor",
                newName: "FarmGreenhouseUnitID");

            migrationBuilder.RenameIndex(
                name: "ix_dataSensor_deviceFarmUnitZone_date",
                table: "dataSensor",
                newName: "ix_dataSensor_farmGreenhouseUnitZone_date");

            migrationBuilder.RenameIndex(
                name: "IX_dataSensor_DeviceFarmUnitID",
                table: "dataSensor",
                newName: "IX_dataSensor_FarmGreenhouseUnitID");

            migrationBuilder.RenameColumn(
                name: "DeviceFarmUnitID",
                table: "farmGreenhouseUnitZone",
                newName: "FarmGreenhouseUnitID");

            migrationBuilder.RenameColumn(
                name: "IDDeviceFarmUnitZone",
                table: "farmGreenhouseUnitZone",
                newName: "IDFarmGreenhouseUnitZone");

            migrationBuilder.RenameIndex(
                name: "IX_deviceFarmUnitZone_DeviceFarmUnitID",
                table: "farmGreenhouseUnitZone",
                newName: "IX_farmGreenhouseUnitZone_FarmGreenhouseUnitID");

            migrationBuilder.RenameColumn(
                name: "DeviceFarmID",
                table: "farmGreenhouseUnit",
                newName: "FarmID");

            migrationBuilder.RenameColumn(
                name: "IDDeviceFarmUnit",
                table: "farmGreenhouseUnit",
                newName: "IDFarmGreenhouseUnit");

            migrationBuilder.RenameIndex(
                name: "IX_deviceFarmUnit_DeviceFarmID",
                table: "farmGreenhouseUnit",
                newName: "IX_farmGreenhouseUnit_FarmID");

            migrationBuilder.RenameColumn(
                name: "IDDeviceFarm",
                table: "farm",
                newName: "IDFarm");

            migrationBuilder.AddPrimaryKey(
                name: "PK_farmGreenhouseUnitZone",
                table: "farmGreenhouseUnitZone",
                column: "IDFarmGreenhouseUnitZone");

            migrationBuilder.AddPrimaryKey(
                name: "PK_farmGreenhouseUnit",
                table: "farmGreenhouseUnit",
                column: "IDFarmGreenhouseUnit");

            migrationBuilder.AddPrimaryKey(
                name: "PK_farm",
                table: "farm",
                column: "IDFarm");

            migrationBuilder.Sql("ALTER TABLE farm MODIFY COLUMN IDFarm int NOT NULL AUTO_INCREMENT;");

            migrationBuilder.AddForeignKey(
                name: "FK_dataSensor_farmGreenhouseUnitZone_FarmGreenhouseUnitZoneID",
                table: "dataSensor",
                column: "FarmGreenhouseUnitZoneID",
                principalTable: "farmGreenhouseUnitZone",
                principalColumn: "IDFarmGreenhouseUnitZone");

            migrationBuilder.AddForeignKey(
                name: "FK_dataSensor_farmGreenhouseUnit_FarmGreenhouseUnitID",
                table: "dataSensor",
                column: "FarmGreenhouseUnitID",
                principalTable: "farmGreenhouseUnit",
                principalColumn: "IDFarmGreenhouseUnit");

            migrationBuilder.AddForeignKey(
                name: "FK_device_farmGreenhouseUnit_FarmGreenhouseUnitID",
                table: "device",
                column: "FarmGreenhouseUnitID",
                principalTable: "farmGreenhouseUnit",
                principalColumn: "IDFarmGreenhouseUnit");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farmGreenhouseUnitZone_DeviceFarmUnit~",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmUnitZoneID",
                principalTable: "farmGreenhouseUnitZone",
                principalColumn: "IDFarmGreenhouseUnitZone");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farmGreenhouseUnit_DeviceFarmUnitID",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmUnitID",
                principalTable: "farmGreenhouseUnit",
                principalColumn: "IDFarmGreenhouseUnit");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farm_DeviceFarmID",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmID",
                principalTable: "farm",
                principalColumn: "IDFarm");

            migrationBuilder.AddForeignKey(
                name: "FK_farmGreenhouseUnit_farm_FarmID",
                table: "farmGreenhouseUnit",
                column: "FarmID",
                principalTable: "farm",
                principalColumn: "IDFarm");

            migrationBuilder.AddForeignKey(
                name: "FK_farmGreenhouseUnitZone_farmGreenhouseUnit_FarmGreenhouseUnit~",
                table: "farmGreenhouseUnitZone",
                column: "FarmGreenhouseUnitID",
                principalTable: "farmGreenhouseUnit",
                principalColumn: "IDFarmGreenhouseUnit");

            migrationBuilder.AddForeignKey(
                name: "FK_ruleNotificationState_farmGreenhouseUnitZone_DeviceFarmUnitZ~",
                table: "ruleNotificationState",
                column: "DeviceFarmUnitZoneID",
                principalTable: "farmGreenhouseUnitZone",
                principalColumn: "IDFarmGreenhouseUnitZone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dataSensor_farmGreenhouseUnitZone_FarmGreenhouseUnitZoneID",
                table: "dataSensor");

            migrationBuilder.DropForeignKey(
                name: "FK_dataSensor_farmGreenhouseUnit_FarmGreenhouseUnitID",
                table: "dataSensor");

            migrationBuilder.DropForeignKey(
                name: "FK_device_farmGreenhouseUnit_FarmGreenhouseUnitID",
                table: "device");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farmGreenhouseUnitZone_DeviceFarmUnit~",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farmGreenhouseUnit_DeviceFarmUnitID",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farm_DeviceFarmID",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropForeignKey(
                name: "FK_farmGreenhouseUnit_farm_FarmID",
                table: "farmGreenhouseUnit");

            migrationBuilder.DropForeignKey(
                name: "FK_farmGreenhouseUnitZone_farmGreenhouseUnit_FarmGreenhouseUnit~",
                table: "farmGreenhouseUnitZone");

            migrationBuilder.DropForeignKey(
                name: "FK_ruleNotificationState_farmGreenhouseUnitZone_DeviceFarmUnitZ~",
                table: "ruleNotificationState");

            migrationBuilder.DropPrimaryKey(
                name: "PK_farmGreenhouseUnitZone",
                table: "farmGreenhouseUnitZone");

            migrationBuilder.DropPrimaryKey(
                name: "PK_farmGreenhouseUnit",
                table: "farmGreenhouseUnit");

            migrationBuilder.Sql("ALTER TABLE farm MODIFY COLUMN IDFarm int NOT NULL;");

            migrationBuilder.DropPrimaryKey(
                name: "PK_farm",
                table: "farm");

            migrationBuilder.RenameTable(
                name: "farmGreenhouseUnitZone",
                newName: "deviceFarmUnitZone");

            migrationBuilder.RenameTable(
                name: "farmGreenhouseUnit",
                newName: "deviceFarmUnit");

            migrationBuilder.RenameTable(
                name: "farm",
                newName: "deviceFarm");

            migrationBuilder.RenameColumn(
                name: "FarmGreenhouseUnitZoneID",
                table: "device",
                newName: "DeviceFarmUnitZoneID");

            migrationBuilder.RenameColumn(
                name: "FarmGreenhouseUnitID",
                table: "device",
                newName: "DeviceFarmUnitID");

            migrationBuilder.RenameIndex(
                name: "IX_device_FarmGreenhouseUnitID",
                table: "device",
                newName: "IX_device_DeviceFarmUnitID");

            migrationBuilder.RenameColumn(
                name: "FarmGreenhouseUnitZoneID",
                table: "dataSensor",
                newName: "DeviceFarmUnitZoneID");

            migrationBuilder.RenameColumn(
                name: "FarmGreenhouseUnitID",
                table: "dataSensor",
                newName: "DeviceFarmUnitID");

            migrationBuilder.RenameIndex(
                name: "ix_dataSensor_farmGreenhouseUnitZone_date",
                table: "dataSensor",
                newName: "ix_dataSensor_deviceFarmUnitZone_date");

            migrationBuilder.RenameIndex(
                name: "IX_dataSensor_FarmGreenhouseUnitID",
                table: "dataSensor",
                newName: "IX_dataSensor_DeviceFarmUnitID");

            migrationBuilder.RenameColumn(
                name: "FarmGreenhouseUnitID",
                table: "deviceFarmUnitZone",
                newName: "DeviceFarmUnitID");

            migrationBuilder.RenameColumn(
                name: "IDFarmGreenhouseUnitZone",
                table: "deviceFarmUnitZone",
                newName: "IDDeviceFarmUnitZone");

            migrationBuilder.RenameIndex(
                name: "IX_farmGreenhouseUnitZone_FarmGreenhouseUnitID",
                table: "deviceFarmUnitZone",
                newName: "IX_deviceFarmUnitZone_DeviceFarmUnitID");

            migrationBuilder.RenameColumn(
                name: "FarmID",
                table: "deviceFarmUnit",
                newName: "DeviceFarmID");

            migrationBuilder.RenameColumn(
                name: "IDFarmGreenhouseUnit",
                table: "deviceFarmUnit",
                newName: "IDDeviceFarmUnit");

            migrationBuilder.RenameIndex(
                name: "IX_farmGreenhouseUnit_FarmID",
                table: "deviceFarmUnit",
                newName: "IX_deviceFarmUnit_DeviceFarmID");

            migrationBuilder.RenameColumn(
                name: "IDFarm",
                table: "deviceFarm",
                newName: "IDDeviceFarm");

            migrationBuilder.AddPrimaryKey(
                name: "PK_deviceFarmUnitZone",
                table: "deviceFarmUnitZone",
                column: "IDDeviceFarmUnitZone");

            migrationBuilder.AddPrimaryKey(
                name: "PK_deviceFarmUnit",
                table: "deviceFarmUnit",
                column: "IDDeviceFarmUnit");

            migrationBuilder.AddPrimaryKey(
                name: "PK_deviceFarm",
                table: "deviceFarm",
                column: "IDDeviceFarm");

            migrationBuilder.Sql("ALTER TABLE deviceFarm MODIFY COLUMN IDDeviceFarm int NOT NULL AUTO_INCREMENT;");

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
                name: "FK_device_deviceFarmUnit_DeviceFarmUnitID",
                table: "device",
                column: "DeviceFarmUnitID",
                principalTable: "deviceFarmUnit",
                principalColumn: "IDDeviceFarmUnit");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnit_deviceFarm_DeviceFarmID",
                table: "deviceFarmUnit",
                column: "DeviceFarmID",
                principalTable: "deviceFarm",
                principalColumn: "IDDeviceFarm");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZone_deviceFarmUnit_DeviceFarmUnitID",
                table: "deviceFarmUnitZone",
                column: "DeviceFarmUnitID",
                principalTable: "deviceFarmUnit",
                principalColumn: "IDDeviceFarmUnit");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_deviceFarmUnitZone_DeviceFarmUnitZone~",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmUnitZoneID",
                principalTable: "deviceFarmUnitZone",
                principalColumn: "IDDeviceFarmUnitZone");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_deviceFarmUnit_DeviceFarmUnitID",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmUnitID",
                principalTable: "deviceFarmUnit",
                principalColumn: "IDDeviceFarmUnit");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_deviceFarm_DeviceFarmID",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmID",
                principalTable: "deviceFarm",
                principalColumn: "IDDeviceFarm");

            migrationBuilder.AddForeignKey(
                name: "FK_ruleNotificationState_deviceFarmUnitZone_DeviceFarmUnitZoneID",
                table: "ruleNotificationState",
                column: "DeviceFarmUnitZoneID",
                principalTable: "deviceFarmUnitZone",
                principalColumn: "IDDeviceFarmUnitZone");
        }
    }
}
