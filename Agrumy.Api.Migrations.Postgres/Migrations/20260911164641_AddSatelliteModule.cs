using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
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
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SatelliteBackfillCompletedUtc",
                table: "farmParcelZone",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "farmParcelZoneSatelliteScene",
                columns: table => new
                {
                    IDFarmParcelZoneSatelliteScene = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FarmParcelZoneID = table.Column<int>(type: "integer", nullable: false),
                    SceneDateUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceSceneId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CloudPercent = table.Column<double>(type: "double precision", nullable: false),
                    ValidPixelPercent = table.Column<double>(type: "double precision", nullable: false),
                    Reliable = table.Column<bool>(type: "boolean", nullable: false),
                    IngestedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmParcelZoneSatelliteScene", x => x.IDFarmParcelZoneSatelliteScene);
                    table.ForeignKey(
                        name: "FK_farmParcelZoneSatelliteScene_farmParcelZone_FarmParcelZoneID",
                        column: x => x.FarmParcelZoneID,
                        principalTable: "farmParcelZone",
                        principalColumn: "IDFarmParcelZone");
                });

            migrationBuilder.CreateTable(
                name: "tenantSatelliteConfig",
                columns: table => new
                {
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ClientSecretEncrypted = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PlanTier = table.Column<int>(type: "integer", nullable: false),
                    DefaultIndicesJson = table.Column<string>(type: "text", nullable: true),
                    MaxCloudPercent = table.Column<int>(type: "integer", nullable: false),
                    MinValidPixelPercent = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastTokenIssuedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastQuotaSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    QuotaPausedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RasterRetentionDaysOverride = table.Column<int>(type: "integer", nullable: true),
                    QuotaPausedNotifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenantSatelliteConfig", x => x.TenantID);
                });

            // Referential integrity only - deliberately not declared to EF's own model (AgrumyDbContext has no
            // HasOne/HasForeignKey for this table), which confuses SaveChanges' key-fixup when TenantID is 0
            // (the real, legitimate bootstrap organization) and is also a shared PK/FK pointing at a store-generated column.
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
                    IDParcelSatelliteIndex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SceneID = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    GridBase64 = table.Column<string>(type: "text", nullable: true),
                    ImagePath = table.Column<string>(type: "text", nullable: true),
                    BoundsJson = table.Column<string>(type: "text", nullable: true),
                    StatsJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parcelSatelliteIndex", x => x.IDParcelSatelliteIndex);
                    table.ForeignKey(
                        name: "FK_parcelSatelliteIndex_farmParcelZoneSatelliteScene_SceneID",
                        column: x => x.SceneID,
                        principalTable: "farmParcelZoneSatelliteScene",
                        principalColumn: "IDFarmParcelZoneSatelliteScene");
                });

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
