using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RestructureOpenFieldR1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dataSensor_farmOpenfieldCropParcel_FarmOpenfieldCropParcelID",
                table: "dataSensor");

            migrationBuilder.DropForeignKey(
                name: "FK_dataSensor_farmOpenfieldCrop_FarmOpenfieldCropID",
                table: "dataSensor");

            migrationBuilder.DropForeignKey(
                name: "FK_device_farmOpenfieldCrop_FarmOpenfieldCropID",
                table: "device");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farmOpenfieldCropParcel_DeviceFarmOp~",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farmOpenfieldCrop_DeviceFarmOpenfiel~",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropTable(
                name: "farmOpenfieldCropParcel");

            migrationBuilder.DropTable(
                name: "farmOpenfieldCrop");

            migrationBuilder.DropIndex(
                name: "IX_device_FarmOpenfieldCropID",
                table: "device");

            migrationBuilder.DropIndex(
                name: "IX_dataSensor_FarmOpenfieldCropID",
                table: "dataSensor");

            migrationBuilder.DropIndex(
                name: "ix_dataSensor_farmOpenfieldCropParcel_date",
                table: "dataSensor");

            migrationBuilder.RenameColumn(
                name: "DeviceFarmOpenfieldCropParcelID",
                table: "deviceFarmUnitZoneRule",
                newName: "DeviceSowingID");

            migrationBuilder.RenameColumn(
                name: "DeviceFarmOpenfieldCropID",
                table: "deviceFarmUnitZoneRule",
                newName: "DeviceFarmParcelZoneID");

            migrationBuilder.RenameIndex(
                name: "ix_deviceFarmUnitZoneRule_parcel",
                table: "deviceFarmUnitZoneRule",
                newName: "ix_deviceFarmUnitZoneRule_sowing");

            migrationBuilder.RenameIndex(
                name: "ix_deviceFarmUnitZoneRule_crop",
                table: "deviceFarmUnitZoneRule",
                newName: "ix_deviceFarmUnitZoneRule_farmParcelZone");

            migrationBuilder.RenameColumn(
                name: "FarmOpenfieldCropParcelID",
                table: "device",
                newName: "SowingID");

            migrationBuilder.RenameColumn(
                name: "FarmOpenfieldCropID",
                table: "device",
                newName: "FarmParcelZoneID");

            migrationBuilder.RenameColumn(
                name: "FarmOpenfieldCropParcelID",
                table: "dataSensor",
                newName: "SowingID");

            migrationBuilder.RenameColumn(
                name: "FarmOpenfieldCropID",
                table: "dataSensor",
                newName: "FarmParcelZoneID");

            migrationBuilder.CreateTable(
                name: "crop",
                columns: table => new
                {
                    IDCrop = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TypicalCycleDays = table.Column<int>(type: "integer", nullable: true),
                    QualityMetricsJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crop", x => x.IDCrop);
                });

            migrationBuilder.CreateTable(
                name: "farmParcel",
                columns: table => new
                {
                    IDFarmParcel = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    FarmOpenfieldID = table.Column<int>(type: "integer", nullable: false),
                    FarmParcelName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmParcel", x => x.IDFarmParcel);
                    table.ForeignKey(
                        name: "FK_farmParcel_farmOpenfield_FarmOpenfieldID",
                        column: x => x.FarmOpenfieldID,
                        principalTable: "farmOpenfield",
                        principalColumn: "IDFarmOpenfield");
                });

            migrationBuilder.CreateTable(
                name: "sowing",
                columns: table => new
                {
                    IDSowing = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    FarmID = table.Column<int>(type: "integer", nullable: false),
                    CropID = table.Column<int>(type: "integer", nullable: false),
                    Variety = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SeedRateKgPerHa = table.Column<double>(type: "double precision", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedDurationDays = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    HarvestDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClosedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserID = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sowing", x => x.IDSowing);
                    table.ForeignKey(
                        name: "FK_sowing_crop_CropID",
                        column: x => x.CropID,
                        principalTable: "crop",
                        principalColumn: "IDCrop");
                    table.ForeignKey(
                        name: "FK_sowing_farm_FarmID",
                        column: x => x.FarmID,
                        principalTable: "farm",
                        principalColumn: "IDFarm");
                });

            migrationBuilder.CreateTable(
                name: "zonePlanting",
                columns: table => new
                {
                    IDZonePlanting = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    DeviceFarmUnitZoneID = table.Column<int>(type: "integer", nullable: false),
                    CropID = table.Column<int>(type: "integer", nullable: false),
                    PlantedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedDurationDays = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    HarvestDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClosedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ActiveDeviceFarmUnitZoneID = table.Column<int>(type: "integer", nullable: true, computedColumnSql: "(CASE WHEN \"Status\" = 2 THEN \"DeviceFarmUnitZoneID\" ELSE NULL END)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zonePlanting", x => x.IDZonePlanting);
                    table.ForeignKey(
                        name: "FK_zonePlanting_crop_CropID",
                        column: x => x.CropID,
                        principalTable: "crop",
                        principalColumn: "IDCrop");
                    table.ForeignKey(
                        name: "FK_zonePlanting_farmGreenhouseUnitZone_DeviceFarmUnitZoneID",
                        column: x => x.DeviceFarmUnitZoneID,
                        principalTable: "farmGreenhouseUnitZone",
                        principalColumn: "IDFarmGreenhouseUnitZone");
                });

            migrationBuilder.CreateTable(
                name: "farmParcelZone",
                columns: table => new
                {
                    IDFarmParcelZone = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    FarmParcelID = table.Column<int>(type: "integer", nullable: false),
                    FarmParcelZoneName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsWholeParcel = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CurrentSowingID = table.Column<int>(type: "integer", nullable: true),
                    WaterPumpMaxRunSeconds = table.Column<int>(type: "integer", nullable: true),
                    WaterPumpCooldownSeconds = table.Column<int>(type: "integer", nullable: true),
                    SkipWaterPumpWhenRainPredicted = table.Column<bool>(type: "boolean", nullable: false),
                    TankCapacityLiters = table.Column<double>(type: "double precision", nullable: true),
                    WaterLevelRawEmpty = table.Column<int>(type: "integer", nullable: true),
                    WaterLevelRawFull = table.Column<int>(type: "integer", nullable: true),
                    TankRefillNotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WaterPumpMinLevel = table.Column<double>(type: "double precision", nullable: true),
                    HeatingMaxRunSeconds = table.Column<int>(type: "integer", nullable: true),
                    VentilationMaxRunSeconds = table.Column<int>(type: "integer", nullable: true),
                    HeatingFailSafePolicy = table.Column<int>(type: "integer", nullable: true),
                    DashboardWidgetsJson = table.Column<string>(type: "text", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmParcelZone", x => x.IDFarmParcelZone);
                    table.ForeignKey(
                        name: "FK_farmParcelZone_farmParcel_FarmParcelID",
                        column: x => x.FarmParcelID,
                        principalTable: "farmParcel",
                        principalColumn: "IDFarmParcel");
                    table.ForeignKey(
                        name: "FK_farmParcelZone_sowing_CurrentSowingID",
                        column: x => x.CurrentSowingID,
                        principalTable: "sowing",
                        principalColumn: "IDSowing");
                });

            migrationBuilder.CreateTable(
                name: "fieldLogEntry",
                columns: table => new
                {
                    IDFieldLogEntry = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    SowingID = table.Column<int>(type: "integer", nullable: true),
                    FarmParcelZoneID = table.Column<int>(type: "integer", nullable: true),
                    ZonePlantingID = table.Column<int>(type: "integer", nullable: true),
                    DeviceFarmUnitZoneID = table.Column<int>(type: "integer", nullable: true),
                    EntryType = table.Column<int>(type: "integer", nullable: false),
                    DateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserID = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    IsClosingEntry = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fieldLogEntry", x => x.IDFieldLogEntry);
                    table.ForeignKey(
                        name: "FK_fieldLogEntry_farmGreenhouseUnitZone_DeviceFarmUnitZoneID",
                        column: x => x.DeviceFarmUnitZoneID,
                        principalTable: "farmGreenhouseUnitZone",
                        principalColumn: "IDFarmGreenhouseUnitZone");
                    table.ForeignKey(
                        name: "FK_fieldLogEntry_farmParcelZone_FarmParcelZoneID",
                        column: x => x.FarmParcelZoneID,
                        principalTable: "farmParcelZone",
                        principalColumn: "IDFarmParcelZone");
                    table.ForeignKey(
                        name: "FK_fieldLogEntry_sowing_SowingID",
                        column: x => x.SowingID,
                        principalTable: "sowing",
                        principalColumn: "IDSowing");
                    table.ForeignKey(
                        name: "FK_fieldLogEntry_zonePlanting_ZonePlantingID",
                        column: x => x.ZonePlantingID,
                        principalTable: "zonePlanting",
                        principalColumn: "IDZonePlanting");
                });

            migrationBuilder.CreateTable(
                name: "harvestResult",
                columns: table => new
                {
                    IDHarvestResult = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SowingID = table.Column<int>(type: "integer", nullable: true),
                    ZonePlantingID = table.Column<int>(type: "integer", nullable: true),
                    FarmParcelZoneID = table.Column<int>(type: "integer", nullable: true),
                    DateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    YieldKg = table.Column<double>(type: "double precision", nullable: false),
                    MoisturePercent = table.Column<double>(type: "double precision", nullable: true),
                    QualityGrade = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LossesKg = table.Column<double>(type: "double precision", nullable: true),
                    MetricsJson = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_harvestResult", x => x.IDHarvestResult);
                    table.ForeignKey(
                        name: "FK_harvestResult_farmParcelZone_FarmParcelZoneID",
                        column: x => x.FarmParcelZoneID,
                        principalTable: "farmParcelZone",
                        principalColumn: "IDFarmParcelZone");
                    table.ForeignKey(
                        name: "FK_harvestResult_sowing_SowingID",
                        column: x => x.SowingID,
                        principalTable: "sowing",
                        principalColumn: "IDSowing");
                    table.ForeignKey(
                        name: "FK_harvestResult_zonePlanting_ZonePlantingID",
                        column: x => x.ZonePlantingID,
                        principalTable: "zonePlanting",
                        principalColumn: "IDZonePlanting");
                });

            migrationBuilder.CreateTable(
                name: "sowingFarmParcelZone",
                columns: table => new
                {
                    SowingID = table.Column<int>(type: "integer", nullable: false),
                    FarmParcelZoneID = table.Column<int>(type: "integer", nullable: false),
                    AssignedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActiveFarmParcelZoneID = table.Column<int>(type: "integer", nullable: true, computedColumnSql: "(CASE WHEN \"ReleasedUtc\" IS NULL THEN \"FarmParcelZoneID\" ELSE NULL END)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sowingFarmParcelZone", x => new { x.SowingID, x.FarmParcelZoneID });
                    table.ForeignKey(
                        name: "FK_sowingFarmParcelZone_farmParcelZone_FarmParcelZoneID",
                        column: x => x.FarmParcelZoneID,
                        principalTable: "farmParcelZone",
                        principalColumn: "IDFarmParcelZone");
                    table.ForeignKey(
                        name: "FK_sowingFarmParcelZone_sowing_SowingID",
                        column: x => x.SowingID,
                        principalTable: "sowing",
                        principalColumn: "IDSowing");
                });

            migrationBuilder.CreateTable(
                name: "fieldLogAttachment",
                columns: table => new
                {
                    IDFieldLogAttachment = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FieldLogEntryID = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fieldLogAttachment", x => x.IDFieldLogAttachment);
                    table.ForeignKey(
                        name: "FK_fieldLogAttachment_fieldLogEntry_FieldLogEntryID",
                        column: x => x.FieldLogEntryID,
                        principalTable: "fieldLogEntry",
                        principalColumn: "IDFieldLogEntry",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_SowingID",
                table: "device",
                column: "SowingID");

            migrationBuilder.CreateIndex(
                name: "ix_dataSensor_farmParcelZone_date",
                table: "dataSensor",
                columns: new[] { "FarmParcelZoneID", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_dataSensor_SowingID",
                table: "dataSensor",
                column: "SowingID");

            migrationBuilder.CreateIndex(
                name: "ix_crop_tenant",
                table: "crop",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "IX_farmParcel_FarmOpenfieldID",
                table: "farmParcel",
                column: "FarmOpenfieldID");

            migrationBuilder.CreateIndex(
                name: "IX_farmParcelZone_CurrentSowingID",
                table: "farmParcelZone",
                column: "CurrentSowingID");

            migrationBuilder.CreateIndex(
                name: "IX_farmParcelZone_FarmParcelID",
                table: "farmParcelZone",
                column: "FarmParcelID");

            migrationBuilder.CreateIndex(
                name: "IX_fieldLogAttachment_FieldLogEntryID",
                table: "fieldLogAttachment",
                column: "FieldLogEntryID");

            migrationBuilder.CreateIndex(
                name: "ix_fieldLogEntry_parcelZone",
                table: "fieldLogEntry",
                column: "FarmParcelZoneID");

            migrationBuilder.CreateIndex(
                name: "ix_fieldLogEntry_sowing",
                table: "fieldLogEntry",
                column: "SowingID");

            migrationBuilder.CreateIndex(
                name: "ix_fieldLogEntry_tenant",
                table: "fieldLogEntry",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "ix_fieldLogEntry_zone",
                table: "fieldLogEntry",
                column: "DeviceFarmUnitZoneID");

            migrationBuilder.CreateIndex(
                name: "ix_fieldLogEntry_zonePlanting",
                table: "fieldLogEntry",
                column: "ZonePlantingID");

            migrationBuilder.CreateIndex(
                name: "IX_harvestResult_FarmParcelZoneID",
                table: "harvestResult",
                column: "FarmParcelZoneID");

            migrationBuilder.CreateIndex(
                name: "ix_harvestResult_sowing",
                table: "harvestResult",
                column: "SowingID");

            migrationBuilder.CreateIndex(
                name: "ix_harvestResult_zonePlanting",
                table: "harvestResult",
                column: "ZonePlantingID");

            migrationBuilder.CreateIndex(
                name: "IX_sowing_CropID",
                table: "sowing",
                column: "CropID");

            migrationBuilder.CreateIndex(
                name: "ix_sowing_farm",
                table: "sowing",
                column: "FarmID");

            migrationBuilder.CreateIndex(
                name: "ix_sowing_tenant",
                table: "sowing",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "ix_sowingFarmParcelZone_zone",
                table: "sowingFarmParcelZone",
                column: "FarmParcelZoneID");

            migrationBuilder.CreateIndex(
                name: "ux_sowingFarmParcelZone_activeZone",
                table: "sowingFarmParcelZone",
                column: "ActiveFarmParcelZoneID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zonePlanting_CropID",
                table: "zonePlanting",
                column: "CropID");

            migrationBuilder.CreateIndex(
                name: "IX_zonePlanting_DeviceFarmUnitZoneID",
                table: "zonePlanting",
                column: "DeviceFarmUnitZoneID");

            migrationBuilder.CreateIndex(
                name: "ux_zonePlanting_activeZone",
                table: "zonePlanting",
                column: "ActiveDeviceFarmUnitZoneID",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_dataSensor_farmParcelZone_FarmParcelZoneID",
                table: "dataSensor",
                column: "FarmParcelZoneID",
                principalTable: "farmParcelZone",
                principalColumn: "IDFarmParcelZone");

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
                name: "FK_deviceFarmUnitZoneRule_farmParcelZone_DeviceFarmParcelZoneID",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmParcelZoneID",
                principalTable: "farmParcelZone",
                principalColumn: "IDFarmParcelZone");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_sowing_DeviceSowingID",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceSowingID",
                principalTable: "sowing",
                principalColumn: "IDSowing");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dataSensor_farmParcelZone_FarmParcelZoneID",
                table: "dataSensor");

            migrationBuilder.DropForeignKey(
                name: "FK_dataSensor_sowing_SowingID",
                table: "dataSensor");

            migrationBuilder.DropForeignKey(
                name: "FK_device_sowing_SowingID",
                table: "device");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farmParcelZone_DeviceFarmParcelZoneID",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_sowing_DeviceSowingID",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropTable(
                name: "fieldLogAttachment");

            migrationBuilder.DropTable(
                name: "harvestResult");

            migrationBuilder.DropTable(
                name: "sowingFarmParcelZone");

            migrationBuilder.DropTable(
                name: "fieldLogEntry");

            migrationBuilder.DropTable(
                name: "farmParcelZone");

            migrationBuilder.DropTable(
                name: "zonePlanting");

            migrationBuilder.DropTable(
                name: "farmParcel");

            migrationBuilder.DropTable(
                name: "sowing");

            migrationBuilder.DropTable(
                name: "crop");

            migrationBuilder.DropIndex(
                name: "IX_device_SowingID",
                table: "device");

            migrationBuilder.DropIndex(
                name: "ix_dataSensor_farmParcelZone_date",
                table: "dataSensor");

            migrationBuilder.DropIndex(
                name: "IX_dataSensor_SowingID",
                table: "dataSensor");

            migrationBuilder.RenameColumn(
                name: "DeviceSowingID",
                table: "deviceFarmUnitZoneRule",
                newName: "DeviceFarmOpenfieldCropParcelID");

            migrationBuilder.RenameColumn(
                name: "DeviceFarmParcelZoneID",
                table: "deviceFarmUnitZoneRule",
                newName: "DeviceFarmOpenfieldCropID");

            migrationBuilder.RenameIndex(
                name: "ix_deviceFarmUnitZoneRule_sowing",
                table: "deviceFarmUnitZoneRule",
                newName: "ix_deviceFarmUnitZoneRule_parcel");

            migrationBuilder.RenameIndex(
                name: "ix_deviceFarmUnitZoneRule_farmParcelZone",
                table: "deviceFarmUnitZoneRule",
                newName: "ix_deviceFarmUnitZoneRule_crop");

            migrationBuilder.RenameColumn(
                name: "SowingID",
                table: "device",
                newName: "FarmOpenfieldCropParcelID");

            migrationBuilder.RenameColumn(
                name: "FarmParcelZoneID",
                table: "device",
                newName: "FarmOpenfieldCropID");

            migrationBuilder.RenameColumn(
                name: "SowingID",
                table: "dataSensor",
                newName: "FarmOpenfieldCropParcelID");

            migrationBuilder.RenameColumn(
                name: "FarmParcelZoneID",
                table: "dataSensor",
                newName: "FarmOpenfieldCropID");

            migrationBuilder.CreateTable(
                name: "farmOpenfieldCrop",
                columns: table => new
                {
                    IDFarmOpenfieldCrop = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FarmOpenfieldCropName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FarmOpenfieldID = table.Column<int>(type: "integer", nullable: false),
                    TenantID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmOpenfieldCrop", x => x.IDFarmOpenfieldCrop);
                    table.ForeignKey(
                        name: "FK_farmOpenfieldCrop_farmOpenfield_FarmOpenfieldID",
                        column: x => x.FarmOpenfieldID,
                        principalTable: "farmOpenfield",
                        principalColumn: "IDFarmOpenfield");
                });

            migrationBuilder.CreateTable(
                name: "farmOpenfieldCropParcel",
                columns: table => new
                {
                    IDFarmOpenfieldCropParcel = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DashboardWidgetsJson = table.Column<string>(type: "text", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FarmOpenfieldCropID = table.Column<int>(type: "integer", nullable: false),
                    FarmOpenfieldCropParcelName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    HeatingFailSafePolicy = table.Column<int>(type: "integer", nullable: true),
                    HeatingMaxRunSeconds = table.Column<int>(type: "integer", nullable: true),
                    SkipWaterPumpWhenRainPredicted = table.Column<bool>(type: "boolean", nullable: false),
                    TankCapacityLiters = table.Column<double>(type: "double precision", nullable: true),
                    TankRefillNotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    VentilationMaxRunSeconds = table.Column<int>(type: "integer", nullable: true),
                    WaterLevelRawEmpty = table.Column<int>(type: "integer", nullable: true),
                    WaterLevelRawFull = table.Column<int>(type: "integer", nullable: true),
                    WaterPumpCooldownSeconds = table.Column<int>(type: "integer", nullable: true),
                    WaterPumpMaxRunSeconds = table.Column<int>(type: "integer", nullable: true),
                    WaterPumpMinLevel = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmOpenfieldCropParcel", x => x.IDFarmOpenfieldCropParcel);
                    table.ForeignKey(
                        name: "FK_farmOpenfieldCropParcel_farmOpenfieldCrop_FarmOpenfieldCrop~",
                        column: x => x.FarmOpenfieldCropID,
                        principalTable: "farmOpenfieldCrop",
                        principalColumn: "IDFarmOpenfieldCrop");
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_FarmOpenfieldCropID",
                table: "device",
                column: "FarmOpenfieldCropID");

            migrationBuilder.CreateIndex(
                name: "IX_dataSensor_FarmOpenfieldCropID",
                table: "dataSensor",
                column: "FarmOpenfieldCropID");

            migrationBuilder.CreateIndex(
                name: "ix_dataSensor_farmOpenfieldCropParcel_date",
                table: "dataSensor",
                columns: new[] { "FarmOpenfieldCropParcelID", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_farmOpenfieldCrop_FarmOpenfieldID",
                table: "farmOpenfieldCrop",
                column: "FarmOpenfieldID");

            migrationBuilder.CreateIndex(
                name: "IX_farmOpenfieldCropParcel_FarmOpenfieldCropID",
                table: "farmOpenfieldCropParcel",
                column: "FarmOpenfieldCropID");

            migrationBuilder.AddForeignKey(
                name: "FK_dataSensor_farmOpenfieldCropParcel_FarmOpenfieldCropParcelID",
                table: "dataSensor",
                column: "FarmOpenfieldCropParcelID",
                principalTable: "farmOpenfieldCropParcel",
                principalColumn: "IDFarmOpenfieldCropParcel");

            migrationBuilder.AddForeignKey(
                name: "FK_dataSensor_farmOpenfieldCrop_FarmOpenfieldCropID",
                table: "dataSensor",
                column: "FarmOpenfieldCropID",
                principalTable: "farmOpenfieldCrop",
                principalColumn: "IDFarmOpenfieldCrop");

            migrationBuilder.AddForeignKey(
                name: "FK_device_farmOpenfieldCrop_FarmOpenfieldCropID",
                table: "device",
                column: "FarmOpenfieldCropID",
                principalTable: "farmOpenfieldCrop",
                principalColumn: "IDFarmOpenfieldCrop");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farmOpenfieldCropParcel_DeviceFarmOp~",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmOpenfieldCropParcelID",
                principalTable: "farmOpenfieldCropParcel",
                principalColumn: "IDFarmOpenfieldCropParcel");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farmOpenfieldCrop_DeviceFarmOpenfiel~",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmOpenfieldCropID",
                principalTable: "farmOpenfieldCrop",
                principalColumn: "IDFarmOpenfieldCrop");
        }
    }
}
