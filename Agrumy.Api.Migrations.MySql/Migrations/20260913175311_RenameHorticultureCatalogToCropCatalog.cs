using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    // Hand-edited: EF's scaffolded diff proposed DropTable+CreateTable for horticultureCatalogCrop/Fruit/CropGrowthStage,
    // which would have discarded live data (invent.hr has rows in the Crop/CropGrowthStage tables) - rewritten as
    // RenameTable/RenameColumn/RenameIndex for those three, keeping genuine DropTable only for the two empty ones.
    public partial class RenameHorticultureCatalogToCropCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_horticultureCatalogCropGrowthStage_horticultureCatalogCrop_H~",
                table: "horticultureCatalogCropGrowthStage");

            migrationBuilder.DropTable(
                name: "horticultureCatalogHydroponic");

            migrationBuilder.DropTable(
                name: "horticultureCatalogPerma");

            migrationBuilder.RenameTable(
                name: "horticultureCatalogCrop",
                newName: "cropCatalogArable");

            migrationBuilder.RenameTable(
                name: "horticultureCatalogFruit",
                newName: "cropCatalogFruit");

            migrationBuilder.RenameTable(
                name: "horticultureCatalogCropGrowthStage",
                newName: "cropCatalogArableGrowthStage");

            migrationBuilder.RenameColumn(
                name: "HorticultureCatalogCropID",
                table: "cropCatalogArableGrowthStage",
                newName: "CropCatalogArableID");

            migrationBuilder.RenameIndex(
                name: "ux_horticultureCatalogCropGrowthStage_crop_stage",
                table: "cropCatalogArableGrowthStage",
                newName: "ux_cropCatalogArableGrowthStage_arable_stage");

            migrationBuilder.AddForeignKey(
                name: "FK_cropCatalogArableGrowthStage_cropCatalogArable_CropCatalogAr~",
                table: "cropCatalogArableGrowthStage",
                column: "CropCatalogArableID",
                principalTable: "cropCatalogArable",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateTable(
                name: "cropCatalogIndustrial",
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
                    table.PrimaryKey("PK_cropCatalogIndustrial", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cropCatalogMedicinalAndAromatic",
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
                    table.PrimaryKey("PK_cropCatalogMedicinalAndAromatic", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cropCatalogOrnamental",
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
                    table.PrimaryKey("PK_cropCatalogOrnamental", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cropCatalogVegetable",
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
                    table.PrimaryKey("PK_cropCatalogVegetable", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cropCatalogIndustrial");

            migrationBuilder.DropTable(
                name: "cropCatalogMedicinalAndAromatic");

            migrationBuilder.DropTable(
                name: "cropCatalogOrnamental");

            migrationBuilder.DropTable(
                name: "cropCatalogVegetable");

            migrationBuilder.DropForeignKey(
                name: "FK_cropCatalogArableGrowthStage_cropCatalogArable_CropCatalogAr~",
                table: "cropCatalogArableGrowthStage");

            migrationBuilder.RenameIndex(
                name: "ux_cropCatalogArableGrowthStage_arable_stage",
                table: "cropCatalogArableGrowthStage",
                newName: "ux_horticultureCatalogCropGrowthStage_crop_stage");

            migrationBuilder.RenameColumn(
                name: "CropCatalogArableID",
                table: "cropCatalogArableGrowthStage",
                newName: "HorticultureCatalogCropID");

            migrationBuilder.RenameTable(
                name: "cropCatalogArableGrowthStage",
                newName: "horticultureCatalogCropGrowthStage");

            migrationBuilder.RenameTable(
                name: "cropCatalogFruit",
                newName: "horticultureCatalogFruit");

            migrationBuilder.RenameTable(
                name: "cropCatalogArable",
                newName: "horticultureCatalogCrop");

            migrationBuilder.AddForeignKey(
                name: "FK_horticultureCatalogCropGrowthStage_horticultureCatalogCrop_H~",
                table: "horticultureCatalogCropGrowthStage",
                column: "HorticultureCatalogCropID",
                principalTable: "horticultureCatalogCrop",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateTable(
                name: "horticultureCatalogHydroponic",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AirHumidityMax = table.Column<double>(type: "double", nullable: true),
                    AirHumidityMin = table.Column<double>(type: "double", nullable: true),
                    AirTempMax = table.Column<double>(type: "double", nullable: true),
                    AirTempMin = table.Column<double>(type: "double", nullable: true),
                    Co2Max = table.Column<double>(type: "double", nullable: true),
                    Co2Min = table.Column<double>(type: "double", nullable: true),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LightMax = table.Column<double>(type: "double", nullable: true),
                    LightMin = table.Column<double>(type: "double", nullable: true),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SoilECMax = table.Column<double>(type: "double", nullable: true),
                    SoilECMin = table.Column<double>(type: "double", nullable: true),
                    SoilMoistureMax = table.Column<double>(type: "double", nullable: true),
                    SoilMoistureMin = table.Column<double>(type: "double", nullable: true),
                    SoilPHMax = table.Column<double>(type: "double", nullable: true),
                    SoilPHMin = table.Column<double>(type: "double", nullable: true),
                    SoilTempMax = table.Column<double>(type: "double", nullable: true),
                    SoilTempMin = table.Column<double>(type: "double", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_horticultureCatalogHydroponic", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "horticultureCatalogPerma",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AirHumidityMax = table.Column<double>(type: "double", nullable: true),
                    AirHumidityMin = table.Column<double>(type: "double", nullable: true),
                    AirTempMax = table.Column<double>(type: "double", nullable: true),
                    AirTempMin = table.Column<double>(type: "double", nullable: true),
                    Co2Max = table.Column<double>(type: "double", nullable: true),
                    Co2Min = table.Column<double>(type: "double", nullable: true),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LightMax = table.Column<double>(type: "double", nullable: true),
                    LightMin = table.Column<double>(type: "double", nullable: true),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SoilECMax = table.Column<double>(type: "double", nullable: true),
                    SoilECMin = table.Column<double>(type: "double", nullable: true),
                    SoilMoistureMax = table.Column<double>(type: "double", nullable: true),
                    SoilMoistureMin = table.Column<double>(type: "double", nullable: true),
                    SoilPHMax = table.Column<double>(type: "double", nullable: true),
                    SoilPHMin = table.Column<double>(type: "double", nullable: true),
                    SoilTempMax = table.Column<double>(type: "double", nullable: true),
                    SoilTempMin = table.Column<double>(type: "double", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_horticultureCatalogPerma", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
