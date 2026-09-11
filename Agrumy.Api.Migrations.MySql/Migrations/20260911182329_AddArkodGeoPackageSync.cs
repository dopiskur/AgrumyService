using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddArkodGeoPackageSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ArkodGeoPackageSyncEnabled",
                table: "serverConfig",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArkodGeoPackageSyncedAtUtc",
                table: "serverConfig",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArkodGeoPackageSyncEnabled",
                table: "serverConfig");

            migrationBuilder.DropColumn(
                name: "ArkodGeoPackageSyncedAtUtc",
                table: "serverConfig");
        }
    }
}
