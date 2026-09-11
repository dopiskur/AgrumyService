using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddSatelliteModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SatelliteRasterRetentionDays",
                table: "serverConfig",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SatelliteBackfillCompletedUtc",
                table: "farmParcelZone",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "farmParcelZoneSatelliteScene",
                columns: table => new
                {
                    IDFarmParcelZoneSatelliteScene = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FarmParcelZoneID = table.Column<int>(type: "int", nullable: false),
                    SceneDateUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceSceneId = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CloudPercent = table.Column<double>(type: "double", nullable: false),
                    ValidPixelPercent = table.Column<double>(type: "double", nullable: false),
                    Reliable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IngestedUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmParcelZoneSatelliteScene", x => x.IDFarmParcelZoneSatelliteScene);
                    table.ForeignKey(
                        name: "FK_farmParcelZoneSatelliteScene_farmParcelZone_FarmParcelZoneID",
                        column: x => x.FarmParcelZoneID,
                        principalTable: "farmParcelZone",
                        principalColumn: "IDFarmParcelZone");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tenantSatelliteConfig",
                columns: table => new
                {
                    TenantID = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClientSecretEncrypted = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PlanTier = table.Column<int>(type: "int", nullable: false),
                    DefaultIndicesJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MaxCloudPercent = table.Column<int>(type: "int", nullable: false),
                    MinValidPixelPercent = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastTokenIssuedUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    LastQuotaSnapshotJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QuotaPausedUntilUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    RasterRetentionDaysOverride = table.Column<int>(type: "int", nullable: true),
                    QuotaPausedNotifiedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenantSatelliteConfig", x => x.TenantID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Referential integrity only - deliberately not declared to EF's own model (AgrumyDbContext has no
            // HasOne/HasForeignKey for this table), which confuses SaveChanges' key-fixup when TenantID is 0
            // (the real, legitimate bootstrap tenant) and is also a shared PK/FK pointing at a store-generated column.
            migrationBuilder.AddForeignKey(
                name: "FK_tenantSatelliteConfig_tenant_TenantID",
                table: "tenantSatelliteConfig",
                column: "TenantID",
                principalTable: "tenant",
                principalColumn: "IDTenant",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateTable(
                name: "parcelSatelliteIndex",
                columns: table => new
                {
                    IDParcelSatelliteIndex = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SceneID = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    GridBase64 = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImagePath = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BoundsJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StatsJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcelSatelliteIndex", x => x.IDParcelSatelliteIndex);
                    table.ForeignKey(
                        name: "FK_parcelSatelliteIndex_farmParcelZoneSatelliteScene_SceneID",
                        column: x => x.SceneID,
                        principalTable: "farmParcelZoneSatelliteScene",
                        principalColumn: "IDFarmParcelZoneSatelliteScene");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ux_farmParcelZoneSatelliteScene_zone_scene",
                table: "farmParcelZoneSatelliteScene",
                columns: new[] { "FarmParcelZoneID", "SourceSceneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_parcelSatelliteIndex_scene_index",
                table: "parcelSatelliteIndex",
                columns: new[] { "SceneID", "Index" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parcelSatelliteIndex");

            migrationBuilder.DropTable(
                name: "tenantSatelliteConfig");

            migrationBuilder.DropTable(
                name: "farmParcelZoneSatelliteScene");

            migrationBuilder.DropColumn(
                name: "SatelliteRasterRetentionDays",
                table: "serverConfig");

            migrationBuilder.DropColumn(
                name: "SatelliteBackfillCompletedUtc",
                table: "farmParcelZone");
        }
    }
}
