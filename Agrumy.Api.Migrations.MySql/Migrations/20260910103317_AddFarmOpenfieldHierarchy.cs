using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmOpenfieldHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeviceFarmOpenfieldCropID",
                table: "deviceFarmUnitZoneRule",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeviceFarmOpenfieldCropParcelID",
                table: "deviceFarmUnitZoneRule",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FarmOpenfieldCropID",
                table: "device",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FarmOpenfieldCropParcelID",
                table: "device",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FarmOpenfieldCropID",
                table: "dataSensor",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FarmOpenfieldCropParcelID",
                table: "dataSensor",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "farmOpenfield",
                columns: table => new
                {
                    IDFarmOpenfield = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TenantID = table.Column<int>(type: "int", nullable: true),
                    FarmID = table.Column<int>(type: "int", nullable: false),
                    Deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmOpenfield", x => x.IDFarmOpenfield);
                    table.ForeignKey(
                        name: "FK_farmOpenfield_farm_FarmID",
                        column: x => x.FarmID,
                        principalTable: "farm",
                        principalColumn: "IDFarm");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "farmOpenfieldCrop",
                columns: table => new
                {
                    IDFarmOpenfieldCrop = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TenantID = table.Column<int>(type: "int", nullable: true),
                    FarmOpenfieldCropName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FarmOpenfieldID = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmOpenfieldCrop", x => x.IDFarmOpenfieldCrop);
                    table.ForeignKey(
                        name: "FK_farmOpenfieldCrop_farmOpenfield_FarmOpenfieldID",
                        column: x => x.FarmOpenfieldID,
                        principalTable: "farmOpenfield",
                        principalColumn: "IDFarmOpenfield");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "farmOpenfieldCropParcel",
                columns: table => new
                {
                    IDFarmOpenfieldCropParcel = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TenantID = table.Column<int>(type: "int", nullable: true),
                    FarmOpenfieldCropID = table.Column<int>(type: "int", nullable: false),
                    FarmOpenfieldCropParcelName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WaterPumpMaxRunSeconds = table.Column<int>(type: "int", nullable: true),
                    WaterPumpCooldownSeconds = table.Column<int>(type: "int", nullable: true),
                    SkipWaterPumpWhenRainPredicted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TankCapacityLiters = table.Column<double>(type: "double", nullable: true),
                    WaterLevelRawEmpty = table.Column<int>(type: "int", nullable: true),
                    WaterLevelRawFull = table.Column<int>(type: "int", nullable: true),
                    TankRefillNotifiedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    WaterPumpMinLevel = table.Column<double>(type: "double", nullable: true),
                    HeatingMaxRunSeconds = table.Column<int>(type: "int", nullable: true),
                    VentilationMaxRunSeconds = table.Column<int>(type: "int", nullable: true),
                    HeatingFailSafePolicy = table.Column<int>(type: "int", nullable: true),
                    DashboardWidgetsJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmOpenfieldCropParcel", x => x.IDFarmOpenfieldCropParcel);
                    table.ForeignKey(
                        name: "FK_farmOpenfieldCropParcel_farmOpenfieldCrop_FarmOpenfieldCropID",
                        column: x => x.FarmOpenfieldCropID,
                        principalTable: "farmOpenfieldCrop",
                        principalColumn: "IDFarmOpenfieldCrop");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_deviceFarmUnitZoneRule_crop",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmOpenfieldCropID");

            migrationBuilder.CreateIndex(
                name: "ix_deviceFarmUnitZoneRule_parcel",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmOpenfieldCropParcelID");

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
                name: "IX_farmOpenfield_FarmID",
                table: "farmOpenfield",
                column: "FarmID");

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
                name: "FK_deviceFarmUnitZoneRule_farmOpenfieldCropParcel_DeviceFarmOpe~",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmOpenfieldCropParcelID",
                principalTable: "farmOpenfieldCropParcel",
                principalColumn: "IDFarmOpenfieldCropParcel");

            migrationBuilder.AddForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farmOpenfieldCrop_DeviceFarmOpenfield~",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmOpenfieldCropID",
                principalTable: "farmOpenfieldCrop",
                principalColumn: "IDFarmOpenfieldCrop");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                name: "FK_deviceFarmUnitZoneRule_farmOpenfieldCropParcel_DeviceFarmOpe~",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropForeignKey(
                name: "FK_deviceFarmUnitZoneRule_farmOpenfieldCrop_DeviceFarmOpenfield~",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropTable(
                name: "farmOpenfieldCropParcel");

            migrationBuilder.DropTable(
                name: "farmOpenfieldCrop");

            migrationBuilder.DropTable(
                name: "farmOpenfield");

            migrationBuilder.DropIndex(
                name: "ix_deviceFarmUnitZoneRule_crop",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropIndex(
                name: "ix_deviceFarmUnitZoneRule_parcel",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropIndex(
                name: "IX_device_FarmOpenfieldCropID",
                table: "device");

            migrationBuilder.DropIndex(
                name: "IX_dataSensor_FarmOpenfieldCropID",
                table: "dataSensor");

            migrationBuilder.DropIndex(
                name: "ix_dataSensor_farmOpenfieldCropParcel_date",
                table: "dataSensor");

            migrationBuilder.DropColumn(
                name: "DeviceFarmOpenfieldCropID",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropColumn(
                name: "DeviceFarmOpenfieldCropParcelID",
                table: "deviceFarmUnitZoneRule");

            migrationBuilder.DropColumn(
                name: "FarmOpenfieldCropID",
                table: "device");

            migrationBuilder.DropColumn(
                name: "FarmOpenfieldCropParcelID",
                table: "device");

            migrationBuilder.DropColumn(
                name: "FarmOpenfieldCropID",
                table: "dataSensor");

            migrationBuilder.DropColumn(
                name: "FarmOpenfieldCropParcelID",
                table: "dataSensor");
        }
    }
}
