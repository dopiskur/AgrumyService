using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
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
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AirTempMin = table.Column<double>(type: "double", nullable: true),
                    AirTempMax = table.Column<double>(type: "double", nullable: true),
                    SoilTempMin = table.Column<double>(type: "double", nullable: true),
                    SoilTempMax = table.Column<double>(type: "double", nullable: true),
                    AirHumidityMin = table.Column<double>(type: "double", nullable: true),
                    AirHumidityMax = table.Column<double>(type: "double", nullable: true),
                    SoilMoistureMin = table.Column<double>(type: "double", nullable: true),
                    SoilMoistureMax = table.Column<double>(type: "double", nullable: true),
                    LightMin = table.Column<double>(type: "double", nullable: true),
                    LightMax = table.Column<double>(type: "double", nullable: true),
                    SoilPHMin = table.Column<double>(type: "double", nullable: true),
                    SoilPHMax = table.Column<double>(type: "double", nullable: true),
                    SoilECMin = table.Column<double>(type: "double", nullable: true),
                    SoilECMax = table.Column<double>(type: "double", nullable: true),
                    Co2Min = table.Column<double>(type: "double", nullable: true),
                    Co2Max = table.Column<double>(type: "double", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_horticultureCatalogFruit", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "horticultureCatalogFruit");
        }
    }
}
