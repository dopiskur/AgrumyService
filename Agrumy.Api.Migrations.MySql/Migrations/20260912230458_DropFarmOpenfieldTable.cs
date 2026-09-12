using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class DropFarmOpenfieldTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_farmParcel_farmOpenfield_FarmOpenfieldID",
                table: "farmParcel");

            migrationBuilder.RenameColumn(
                name: "FarmOpenfieldID",
                table: "farmParcel",
                newName: "FarmID");

            migrationBuilder.RenameIndex(
                name: "IX_farmParcel_FarmOpenfieldID",
                table: "farmParcel",
                newName: "IX_farmParcel_FarmID");

            // The rename above is a pure relabel - FarmID still holds the old FarmOpenfieldID values here, so backfill the real Farm id via farmOpenfield before it's dropped below.
            migrationBuilder.Sql("""
                UPDATE `farmParcel` p
                JOIN `farmOpenfield` o ON p.`FarmID` = o.`IDFarmOpenfield`
                SET p.`FarmID` = o.`FarmID`;
                """);

            migrationBuilder.DropTable(
                name: "farmOpenfield");

            migrationBuilder.AddForeignKey(
                name: "FK_farmParcel_farm_FarmID",
                table: "farmParcel",
                column: "FarmID",
                principalTable: "farm",
                principalColumn: "IDFarm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_farmParcel_farm_FarmID",
                table: "farmParcel");

            migrationBuilder.CreateTable(
                name: "farmOpenfield",
                columns: table => new
                {
                    IDFarmOpenfield = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    FarmID = table.Column<int>(type: "int", nullable: false),
                    TenantID = table.Column<int>(type: "int", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_farmOpenfield_FarmID",
                table: "farmOpenfield",
                column: "FarmID");

            // One farmOpenfield row per distinct Farm that currently has at least one parcel - reverse of Up()'s backfill, restoring the 1:1 extension-row shape.
            migrationBuilder.Sql("""
                INSERT INTO `farmOpenfield` (`FarmID`, `TenantID`)
                SELECT DISTINCT p.`FarmID`, f.`TenantID`
                FROM `farmParcel` p
                JOIN `farm` f ON f.`IDFarm` = p.`FarmID`;
                """);

            migrationBuilder.RenameColumn(
                name: "FarmID",
                table: "farmParcel",
                newName: "FarmOpenfieldID");

            migrationBuilder.RenameIndex(
                name: "IX_farmParcel_FarmID",
                table: "farmParcel",
                newName: "IX_farmParcel_FarmOpenfieldID");

            // FarmOpenfieldID (just renamed from FarmID) still holds Farm ids here - point it at the matching farmOpenfield row's own id instead.
            migrationBuilder.Sql("""
                UPDATE `farmParcel` p
                JOIN `farmOpenfield` o ON p.`FarmOpenfieldID` = o.`FarmID`
                SET p.`FarmOpenfieldID` = o.`IDFarmOpenfield`;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_farmParcel_farmOpenfield_FarmOpenfieldID",
                table: "farmParcel",
                column: "FarmOpenfieldID",
                principalTable: "farmOpenfield",
                principalColumn: "IDFarmOpenfield");
        }
    }
}
