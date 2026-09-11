using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddParcelGeometry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AreaHectares",
                table: "farmParcelZone",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BboxMaxLat",
                table: "farmParcelZone",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BboxMaxLon",
                table: "farmParcelZone",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BboxMinLat",
                table: "farmParcelZone",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BboxMinLon",
                table: "farmParcelZone",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeometryGeoJson",
                table: "farmParcelZone",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AreaHectares",
                table: "farmParcel",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArkodParcelId",
                table: "farmParcel",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BboxMaxLat",
                table: "farmParcel",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BboxMaxLon",
                table: "farmParcel",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BboxMinLat",
                table: "farmParcel",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BboxMinLon",
                table: "farmParcel",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeometryGeoJson",
                table: "farmParcel",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AreaHectares",
                table: "farmParcelZone");

            migrationBuilder.DropColumn(
                name: "BboxMaxLat",
                table: "farmParcelZone");

            migrationBuilder.DropColumn(
                name: "BboxMaxLon",
                table: "farmParcelZone");

            migrationBuilder.DropColumn(
                name: "BboxMinLat",
                table: "farmParcelZone");

            migrationBuilder.DropColumn(
                name: "BboxMinLon",
                table: "farmParcelZone");

            migrationBuilder.DropColumn(
                name: "GeometryGeoJson",
                table: "farmParcelZone");

            migrationBuilder.DropColumn(
                name: "AreaHectares",
                table: "farmParcel");

            migrationBuilder.DropColumn(
                name: "ArkodParcelId",
                table: "farmParcel");

            migrationBuilder.DropColumn(
                name: "BboxMaxLat",
                table: "farmParcel");

            migrationBuilder.DropColumn(
                name: "BboxMaxLon",
                table: "farmParcel");

            migrationBuilder.DropColumn(
                name: "BboxMinLat",
                table: "farmParcel");

            migrationBuilder.DropColumn(
                name: "BboxMinLon",
                table: "farmParcel");

            migrationBuilder.DropColumn(
                name: "GeometryGeoJson",
                table: "farmParcel");
        }
    }
}
