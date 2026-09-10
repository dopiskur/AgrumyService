using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddHorticultureCatalogFruit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "horticultureCatalogFruit",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AirTempMin = table.Column<double>(type: "double precision", nullable: true),
                    AirTempMax = table.Column<double>(type: "double precision", nullable: true),
                    SoilTempMin = table.Column<double>(type: "double precision", nullable: true),
                    SoilTempMax = table.Column<double>(type: "double precision", nullable: true),
                    AirHumidityMin = table.Column<double>(type: "double precision", nullable: true),
                    AirHumidityMax = table.Column<double>(type: "double precision", nullable: true),
                    SoilMoistureMin = table.Column<double>(type: "double precision", nullable: true),
                    SoilMoistureMax = table.Column<double>(type: "double precision", nullable: true),
                    LightMin = table.Column<double>(type: "double precision", nullable: true),
                    LightMax = table.Column<double>(type: "double precision", nullable: true),
                    SoilPHMin = table.Column<double>(type: "double precision", nullable: true),
                    SoilPHMax = table.Column<double>(type: "double precision", nullable: true),
                    SoilECMin = table.Column<double>(type: "double precision", nullable: true),
                    SoilECMax = table.Column<double>(type: "double precision", nullable: true),
                    Co2Min = table.Column<double>(type: "double precision", nullable: true),
                    Co2Max = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_horticultureCatalogFruit", x => x.ID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "horticultureCatalogFruit");
        }
    }
}
