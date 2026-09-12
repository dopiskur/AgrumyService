using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class RenameFarmHierarchyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dataSensor_sowing_SowingID",
                table: "dataSensor");

            migrationBuilder.DropForeignKey(
                name: "FK_device_sowing_SowingID",
                table: "device");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_sowing_DeviceSowingID",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropForeignKey(
                name: "FK_eventDevice_eventType_EventID",
                table: "eventDevice");

            migrationBuilder.DropForeignKey(
                name: "FK_farmParcelZone_sowing_CurrentSowingID",
                table: "farmParcelZone");

            migrationBuilder.DropForeignKey(
                name: "FK_fieldLogEntry_sowing_SowingID",
                table: "fieldLogEntry");

            migrationBuilder.DropForeignKey(
                name: "FK_fieldLogEntry_zonePlanting_ZonePlantingID",
                table: "fieldLogEntry");

            migrationBuilder.DropForeignKey(
                name: "FK_harvestResult_sowing_SowingID",
                table: "harvestResult");

            migrationBuilder.DropForeignKey(
                name: "FK_harvestResult_zonePlanting_ZonePlantingID",
                table: "harvestResult");

            migrationBuilder.DropForeignKey(
                name: "FK_parcelSatelliteIndex_farmParcelZoneSatelliteScene_SceneID",
                table: "parcelSatelliteIndex");

            migrationBuilder.DropForeignKey(
                name: "FK_sowing_crop_CropID",
                table: "sowing");

            migrationBuilder.DropForeignKey(
                name: "FK_sowing_farm_FarmID",
                table: "sowing");

            migrationBuilder.DropForeignKey(
                name: "FK_sowingFarmParcelZone_farmParcelZone_FarmParcelZoneID",
                table: "sowingFarmParcelZone");

            migrationBuilder.DropForeignKey(
                name: "FK_sowingFarmParcelZone_sowing_SowingID",
                table: "sowingFarmParcelZone");

            migrationBuilder.DropForeignKey(
                name: "FK_zonePlanting_crop_CropID",
                table: "zonePlanting");

            migrationBuilder.DropForeignKey(
                name: "FK_zonePlanting_farmGreenhouseUnitZone_DeviceFarmUnitZoneID",
                table: "zonePlanting");

            migrationBuilder.DropPrimaryKey(
                name: "PK_zonePlanting",
                table: "zonePlanting");

            migrationBuilder.DropPrimaryKey(
                name: "PK_sowingFarmParcelZone",
                table: "sowingFarmParcelZone");

            migrationBuilder.DropPrimaryKey(
                name: "PK_sowing",
                table: "sowing");

            migrationBuilder.DropPrimaryKey(
                name: "PK_parcelSatelliteIndex",
                table: "parcelSatelliteIndex");

            migrationBuilder.DropPrimaryKey(
                name: "PK_eventDevice",
                table: "eventDevice");

            migrationBuilder.RenameTable(
                name: "zonePlanting",
                newName: "farmGreenhouseUnitZonePlanting");

            migrationBuilder.RenameTable(
                name: "sowingFarmParcelZone",
                newName: "farmParcelZoneSowing");

            migrationBuilder.RenameTable(
                name: "sowing",
                newName: "farmSowing");

            migrationBuilder.RenameTable(
                name: "parcelSatelliteIndex",
                newName: "farmParcelZoneSatelliteSceneIndex");

            migrationBuilder.RenameTable(
                name: "eventDevice",
                newName: "deviceEvent");

            migrationBuilder.RenameIndex(
                name: "ux_zonePlanting_activeZone",
                table: "farmGreenhouseUnitZonePlanting",
                newName: "ux_farmGreenhouseUnitZonePlanting_activeZone");

            migrationBuilder.RenameIndex(
                name: "IX_zonePlanting_DeviceFarmUnitZoneID",
                table: "farmGreenhouseUnitZonePlanting",
                newName: "IX_farmGreenhouseUnitZonePlanting_DeviceFarmUnitZoneID");

            migrationBuilder.RenameIndex(
                name: "IX_zonePlanting_CropID",
                table: "farmGreenhouseUnitZonePlanting",
                newName: "IX_farmGreenhouseUnitZonePlanting_CropID");

            migrationBuilder.RenameIndex(
                name: "ux_sowingFarmParcelZone_activeZone",
                table: "farmParcelZoneSowing",
                newName: "ux_farmParcelZoneSowing_activeZone");

            migrationBuilder.RenameIndex(
                name: "ix_sowingFarmParcelZone_zone",
                table: "farmParcelZoneSowing",
                newName: "ix_farmParcelZoneSowing_zone");

            migrationBuilder.RenameIndex(
                name: "ix_sowing_tenant",
                table: "farmSowing",
                newName: "ix_farmSowing_tenant");

            migrationBuilder.RenameIndex(
                name: "ix_sowing_farm",
                table: "farmSowing",
                newName: "ix_farmSowing_farm");

            migrationBuilder.RenameIndex(
                name: "IX_sowing_CropID",
                table: "farmSowing",
                newName: "IX_farmSowing_CropID");

            migrationBuilder.RenameIndex(
                name: "ux_parcelSatelliteIndex_scene_index",
                table: "farmParcelZoneSatelliteSceneIndex",
                newName: "ux_farmParcelZoneSatelliteSceneIndex_scene_index");

            migrationBuilder.RenameIndex(
                name: "IX_eventDevice_EventID",
                table: "deviceEvent",
                newName: "IX_deviceEvent_EventID");

            migrationBuilder.RenameIndex(
                name: "ix_eventDevice_device_date",
                table: "deviceEvent",
                newName: "ix_deviceEvent_device_date");

            migrationBuilder.AddPrimaryKey(
                name: "PK_farmGreenhouseUnitZonePlanting",
                table: "farmGreenhouseUnitZonePlanting",
                column: "IDZonePlanting");

            migrationBuilder.AddPrimaryKey(
                name: "PK_farmParcelZoneSowing",
                table: "farmParcelZoneSowing",
                columns: new[] { "SowingID", "FarmParcelZoneID" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_farmSowing",
                table: "farmSowing",
                column: "IDSowing");

            migrationBuilder.AddPrimaryKey(
                name: "PK_farmParcelZoneSatelliteSceneIndex",
                table: "farmParcelZoneSatelliteSceneIndex",
                column: "IDParcelSatelliteIndex");

            migrationBuilder.AddPrimaryKey(
                name: "PK_deviceEvent",
                table: "deviceEvent",
                column: "IDEventDevice");

            migrationBuilder.AddForeignKey(
                name: "FK_dataSensor_farmSowing_SowingID",
                table: "dataSensor",
                column: "SowingID",
                principalTable: "farmSowing",
                principalColumn: "IDSowing");

            migrationBuilder.AddForeignKey(
                name: "FK_device_farmSowing_SowingID",
                table: "device",
                column: "SowingID",
                principalTable: "farmSowing",
                principalColumn: "IDSowing");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceEvent_eventType_EventID",
                table: "deviceEvent",
                column: "EventID",
                principalTable: "eventType",
                principalColumn: "IDEventType");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farmSowing_DeviceSowingID",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceSowingID",
                principalTable: "farmSowing",
                principalColumn: "IDSowing");

            migrationBuilder.AddForeignKey(
                name: "FK_farmGreenhouseUnitZonePlanting_crop_CropID",
                table: "farmGreenhouseUnitZonePlanting",
                column: "CropID",
                principalTable: "crop",
                principalColumn: "IDCrop");

            migrationBuilder.AddForeignKey(
                name: "FK_farmGreenhouseUnitZonePlanting_farmGreenhouseUnitZone_Device~",
                table: "farmGreenhouseUnitZonePlanting",
                column: "DeviceFarmUnitZoneID",
                principalTable: "farmGreenhouseUnitZone",
                principalColumn: "IDFarmGreenhouseUnitZone");

            migrationBuilder.AddForeignKey(
                name: "FK_farmParcelZone_farmSowing_CurrentSowingID",
                table: "farmParcelZone",
                column: "CurrentSowingID",
                principalTable: "farmSowing",
                principalColumn: "IDSowing");

            migrationBuilder.AddForeignKey(
                name: "FK_farmParcelZoneSatelliteSceneIndex_farmParcelZoneSatelliteSce~",
                table: "farmParcelZoneSatelliteSceneIndex",
                column: "SceneID",
                principalTable: "farmParcelZoneSatelliteScene",
                principalColumn: "IDFarmParcelZoneSatelliteScene");

            migrationBuilder.AddForeignKey(
                name: "FK_farmParcelZoneSowing_farmParcelZone_FarmParcelZoneID",
                table: "farmParcelZoneSowing",
                column: "FarmParcelZoneID",
                principalTable: "farmParcelZone",
                principalColumn: "IDFarmParcelZone");

            migrationBuilder.AddForeignKey(
                name: "FK_farmParcelZoneSowing_farmSowing_SowingID",
                table: "farmParcelZoneSowing",
                column: "SowingID",
                principalTable: "farmSowing",
                principalColumn: "IDSowing");

            migrationBuilder.AddForeignKey(
                name: "FK_farmSowing_crop_CropID",
                table: "farmSowing",
                column: "CropID",
                principalTable: "crop",
                principalColumn: "IDCrop");

            migrationBuilder.AddForeignKey(
                name: "FK_farmSowing_farm_FarmID",
                table: "farmSowing",
                column: "FarmID",
                principalTable: "farm",
                principalColumn: "IDFarm");

            migrationBuilder.AddForeignKey(
                name: "FK_fieldLogEntry_farmGreenhouseUnitZonePlanting_ZonePlantingID",
                table: "fieldLogEntry",
                column: "ZonePlantingID",
                principalTable: "farmGreenhouseUnitZonePlanting",
                principalColumn: "IDZonePlanting");

            migrationBuilder.AddForeignKey(
                name: "FK_fieldLogEntry_farmSowing_SowingID",
                table: "fieldLogEntry",
                column: "SowingID",
                principalTable: "farmSowing",
                principalColumn: "IDSowing");

            migrationBuilder.AddForeignKey(
                name: "FK_harvestResult_farmGreenhouseUnitZonePlanting_ZonePlantingID",
                table: "harvestResult",
                column: "ZonePlantingID",
                principalTable: "farmGreenhouseUnitZonePlanting",
                principalColumn: "IDZonePlanting");

            migrationBuilder.AddForeignKey(
                name: "FK_harvestResult_farmSowing_SowingID",
                table: "harvestResult",
                column: "SowingID",
                principalTable: "farmSowing",
                principalColumn: "IDSowing");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dataSensor_farmSowing_SowingID",
                table: "dataSensor");

            migrationBuilder.DropForeignKey(
                name: "FK_device_farmSowing_SowingID",
                table: "device");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceEvent_eventType_EventID",
                table: "deviceEvent");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farmSowing_DeviceSowingID",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropForeignKey(
                name: "FK_farmGreenhouseUnitZonePlanting_crop_CropID",
                table: "farmGreenhouseUnitZonePlanting");

            migrationBuilder.DropForeignKey(
                name: "FK_farmGreenhouseUnitZonePlanting_farmGreenhouseUnitZone_Device~",
                table: "farmGreenhouseUnitZonePlanting");

            migrationBuilder.DropForeignKey(
                name: "FK_farmParcelZone_farmSowing_CurrentSowingID",
                table: "farmParcelZone");

            migrationBuilder.DropForeignKey(
                name: "FK_farmParcelZoneSatelliteSceneIndex_farmParcelZoneSatelliteSce~",
                table: "farmParcelZoneSatelliteSceneIndex");

            migrationBuilder.DropForeignKey(
                name: "FK_farmParcelZoneSowing_farmParcelZone_FarmParcelZoneID",
                table: "farmParcelZoneSowing");

            migrationBuilder.DropForeignKey(
                name: "FK_farmParcelZoneSowing_farmSowing_SowingID",
                table: "farmParcelZoneSowing");

            migrationBuilder.DropForeignKey(
                name: "FK_farmSowing_crop_CropID",
                table: "farmSowing");

            migrationBuilder.DropForeignKey(
                name: "FK_farmSowing_farm_FarmID",
                table: "farmSowing");

            migrationBuilder.DropForeignKey(
                name: "FK_fieldLogEntry_farmGreenhouseUnitZonePlanting_ZonePlantingID",
                table: "fieldLogEntry");

            migrationBuilder.DropForeignKey(
                name: "FK_fieldLogEntry_farmSowing_SowingID",
                table: "fieldLogEntry");

            migrationBuilder.DropForeignKey(
                name: "FK_harvestResult_farmGreenhouseUnitZonePlanting_ZonePlantingID",
                table: "harvestResult");

            migrationBuilder.DropForeignKey(
                name: "FK_harvestResult_farmSowing_SowingID",
                table: "harvestResult");

            migrationBuilder.DropPrimaryKey(
                name: "PK_farmSowing",
                table: "farmSowing");

            migrationBuilder.DropPrimaryKey(
                name: "PK_farmParcelZoneSowing",
                table: "farmParcelZoneSowing");

            migrationBuilder.DropPrimaryKey(
                name: "PK_farmParcelZoneSatelliteSceneIndex",
                table: "farmParcelZoneSatelliteSceneIndex");

            migrationBuilder.DropPrimaryKey(
                name: "PK_farmGreenhouseUnitZonePlanting",
                table: "farmGreenhouseUnitZonePlanting");

            migrationBuilder.DropPrimaryKey(
                name: "PK_deviceEvent",
                table: "deviceEvent");

            migrationBuilder.RenameTable(
                name: "farmSowing",
                newName: "sowing");

            migrationBuilder.RenameTable(
                name: "farmParcelZoneSowing",
                newName: "sowingFarmParcelZone");

            migrationBuilder.RenameTable(
                name: "farmParcelZoneSatelliteSceneIndex",
                newName: "parcelSatelliteIndex");

            migrationBuilder.RenameTable(
                name: "farmGreenhouseUnitZonePlanting",
                newName: "zonePlanting");

            migrationBuilder.RenameTable(
                name: "deviceEvent",
                newName: "eventDevice");

            migrationBuilder.RenameIndex(
                name: "ix_farmSowing_tenant",
                table: "sowing",
                newName: "ix_sowing_tenant");

            migrationBuilder.RenameIndex(
                name: "ix_farmSowing_farm",
                table: "sowing",
                newName: "ix_sowing_farm");

            migrationBuilder.RenameIndex(
                name: "IX_farmSowing_CropID",
                table: "sowing",
                newName: "IX_sowing_CropID");

            migrationBuilder.RenameIndex(
                name: "ux_farmParcelZoneSowing_activeZone",
                table: "sowingFarmParcelZone",
                newName: "ux_sowingFarmParcelZone_activeZone");

            migrationBuilder.RenameIndex(
                name: "ix_farmParcelZoneSowing_zone",
                table: "sowingFarmParcelZone",
                newName: "ix_sowingFarmParcelZone_zone");

            migrationBuilder.RenameIndex(
                name: "ux_farmParcelZoneSatelliteSceneIndex_scene_index",
                table: "parcelSatelliteIndex",
                newName: "ux_parcelSatelliteIndex_scene_index");

            migrationBuilder.RenameIndex(
                name: "ux_farmGreenhouseUnitZonePlanting_activeZone",
                table: "zonePlanting",
                newName: "ux_zonePlanting_activeZone");

            migrationBuilder.RenameIndex(
                name: "IX_farmGreenhouseUnitZonePlanting_DeviceFarmUnitZoneID",
                table: "zonePlanting",
                newName: "IX_zonePlanting_DeviceFarmUnitZoneID");

            migrationBuilder.RenameIndex(
                name: "IX_farmGreenhouseUnitZonePlanting_CropID",
                table: "zonePlanting",
                newName: "IX_zonePlanting_CropID");

            migrationBuilder.RenameIndex(
                name: "IX_deviceEvent_EventID",
                table: "eventDevice",
                newName: "IX_eventDevice_EventID");

            migrationBuilder.RenameIndex(
                name: "ix_deviceEvent_device_date",
                table: "eventDevice",
                newName: "ix_eventDevice_device_date");

            migrationBuilder.AddPrimaryKey(
                name: "PK_sowing",
                table: "sowing",
                column: "IDSowing");

            migrationBuilder.AddPrimaryKey(
                name: "PK_sowingFarmParcelZone",
                table: "sowingFarmParcelZone",
                columns: new[] { "SowingID", "FarmParcelZoneID" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_parcelSatelliteIndex",
                table: "parcelSatelliteIndex",
                column: "IDParcelSatelliteIndex");

            migrationBuilder.AddPrimaryKey(
                name: "PK_zonePlanting",
                table: "zonePlanting",
                column: "IDZonePlanting");

            migrationBuilder.AddPrimaryKey(
                name: "PK_eventDevice",
                table: "eventDevice",
                column: "IDEventDevice");

            migrationBuilder.AddForeignKey(
                name: "FK_dataSensor_sowing_SowingID",
                table: "dataSensor",
                column: "SowingID",
                principalTable: "sowing",
                principalColumn: "IDSowing");

            migrationBuilder.AddForeignKey(
                name: "FK_device_sowing_SowingID",
                table: "device",
                column: "SowingID",
                principalTable: "sowing",
                principalColumn: "IDSowing");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_sowing_DeviceSowingID",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceSowingID",
                principalTable: "sowing",
                principalColumn: "IDSowing");

            migrationBuilder.AddForeignKey(
                name: "FK_eventDevice_eventType_EventID",
                table: "eventDevice",
                column: "EventID",
                principalTable: "eventType",
                principalColumn: "IDEventType");

            migrationBuilder.AddForeignKey(
                name: "FK_farmParcelZone_sowing_CurrentSowingID",
                table: "farmParcelZone",
                column: "CurrentSowingID",
                principalTable: "sowing",
                principalColumn: "IDSowing");

            migrationBuilder.AddForeignKey(
                name: "FK_fieldLogEntry_sowing_SowingID",
                table: "fieldLogEntry",
                column: "SowingID",
                principalTable: "sowing",
                principalColumn: "IDSowing");

            migrationBuilder.AddForeignKey(
                name: "FK_fieldLogEntry_zonePlanting_ZonePlantingID",
                table: "fieldLogEntry",
                column: "ZonePlantingID",
                principalTable: "zonePlanting",
                principalColumn: "IDZonePlanting");

            migrationBuilder.AddForeignKey(
                name: "FK_harvestResult_sowing_SowingID",
                table: "harvestResult",
                column: "SowingID",
                principalTable: "sowing",
                principalColumn: "IDSowing");

            migrationBuilder.AddForeignKey(
                name: "FK_harvestResult_zonePlanting_ZonePlantingID",
                table: "harvestResult",
                column: "ZonePlantingID",
                principalTable: "zonePlanting",
                principalColumn: "IDZonePlanting");

            migrationBuilder.AddForeignKey(
                name: "FK_parcelSatelliteIndex_farmParcelZoneSatelliteScene_SceneID",
                table: "parcelSatelliteIndex",
                column: "SceneID",
                principalTable: "farmParcelZoneSatelliteScene",
                principalColumn: "IDFarmParcelZoneSatelliteScene");

            migrationBuilder.AddForeignKey(
                name: "FK_sowing_crop_CropID",
                table: "sowing",
                column: "CropID",
                principalTable: "crop",
                principalColumn: "IDCrop");

            migrationBuilder.AddForeignKey(
                name: "FK_sowing_farm_FarmID",
                table: "sowing",
                column: "FarmID",
                principalTable: "farm",
                principalColumn: "IDFarm");

            migrationBuilder.AddForeignKey(
                name: "FK_sowingFarmParcelZone_farmParcelZone_FarmParcelZoneID",
                table: "sowingFarmParcelZone",
                column: "FarmParcelZoneID",
                principalTable: "farmParcelZone",
                principalColumn: "IDFarmParcelZone");

            migrationBuilder.AddForeignKey(
                name: "FK_sowingFarmParcelZone_sowing_SowingID",
                table: "sowingFarmParcelZone",
                column: "SowingID",
                principalTable: "sowing",
                principalColumn: "IDSowing");

            migrationBuilder.AddForeignKey(
                name: "FK_zonePlanting_crop_CropID",
                table: "zonePlanting",
                column: "CropID",
                principalTable: "crop",
                principalColumn: "IDCrop");

            migrationBuilder.AddForeignKey(
                name: "FK_zonePlanting_farmGreenhouseUnitZone_DeviceFarmUnitZoneID",
                table: "zonePlanting",
                column: "DeviceFarmUnitZoneID",
                principalTable: "farmGreenhouseUnitZone",
                principalColumn: "IDFarmGreenhouseUnitZone");
        }
    }
}
