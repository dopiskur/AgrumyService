using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddHorticultureCropGrowthStages : Migration
    {
        // Global reference data (horticultureCatalogCrop has no TenantID) - Croatian wheat quality classes (Pravilnik o kvalitativnim klasama pšenice) and FAO maize maturity numbers, BBCH-staged (0-9; maize's own BBCH monograph never defines 2/Tillering or 4/Booting, so those two are simply absent per entry). Looked up by Name via subquery rather than hardcoded ids - portable across providers without relying on LAST_INSERT_ID()/RETURNING, and safe regardless of whatever ids already exist.
        private const string SeedSql = """
            INSERT INTO "horticultureCatalogCrop" ("Name", "Description", "ClassCode", "PhaseDescriptionsJson") VALUES
            ('Wheat - Winter (Bc Anica)', 'Croatian winter wheat cultivar, early maturing, excellent lodging resistance.', 'B1/A2', '{"0":"Germination - seed absorbs water, radicle and coleoptile emerge.","1":"Leaf development - first leaves unfold on the main shoot.","2":"Tillering - side shoots (tillers) form at the base.","3":"Stem elongation - internodes lengthen, canopy grows upright.","4":"Booting - flag leaf sheath swells around the developing ear.","5":"Heading - the ear emerges from the flag leaf sheath.","6":"Flowering (anthesis) - most sensitive stage, avoid heat/frost/drought stress.","7":"Milk development - grain fills with a milky liquid.","8":"Dough development - grain firms into a soft, then hard dough.","9":"Ripening - grain dries and hardens, ready for harvest near the end of this stage."}'),
            ('Wheat - Winter (SJ. Tena)', 'Croatian winter wheat cultivar, high protein content.', 'A1/A2', '{"0":"Germination - seed absorbs water, radicle and coleoptile emerge.","1":"Leaf development - first leaves unfold on the main shoot.","2":"Tillering - side shoots (tillers) form at the base.","3":"Stem elongation - internodes lengthen, canopy grows upright.","4":"Booting - flag leaf sheath swells around the developing ear.","5":"Heading - the ear emerges from the flag leaf sheath.","6":"Flowering (anthesis) - most sensitive stage, avoid heat/frost/drought stress.","7":"Milk development - grain fills with a milky liquid.","8":"Dough development - grain firms into a soft, then hard dough.","9":"Ripening - grain dries and hardens, ready for harvest near the end of this stage."}'),
            ('Wheat - Spring', 'Generic spring wheat - shorter cycle, no vernalization requirement, better drought/heat tolerance than winter wheat.', NULL, '{"0":"Germination - seed absorbs water, radicle and coleoptile emerge.","1":"Leaf development - first leaves unfold on the main shoot.","2":"Tillering - side shoots (tillers) form at the base.","3":"Stem elongation - internodes lengthen, canopy grows upright.","4":"Booting - flag leaf sheath swells around the developing ear.","5":"Heading - the ear emerges from the flag leaf sheath.","6":"Flowering (anthesis) - most sensitive stage, avoid heat/frost/drought stress.","7":"Milk development - grain fills with a milky liquid.","8":"Dough development - grain firms into a soft, then hard dough.","9":"Ripening - grain dries and hardens, ready for harvest near the end of this stage."}'),
            ('Corn - FAO 300', 'Early-maturing maize hybrid, roughly 90-100 days total cycle.', 'FAO 300', '{"0":"Germination - kernel absorbs water, radicle and coleoptile emerge; needs soil above 10C.","1":"Leaf development - successive leaves emerge (V-stages).","3":"Stem elongation - rapid vegetative growth toward tasseling.","5":"Tasseling - the tassel fully emerges above the canopy.","6":"Flowering (silking/pollination) - most water-sensitive stage, avoid any drought stress.","7":"Development of fruit - kernels fill with a milky liquid.","8":"Ripening - kernels progress through dough, dent, and physiological maturity (black layer).","9":"Senescence - plant dries down, kernels hard, ready for harvest."}'),
            ('Corn - FAO 500', 'Mid-to-late maize hybrid, roughly 115-125 days total cycle.', 'FAO 500', '{"0":"Germination - kernel absorbs water, radicle and coleoptile emerge; needs soil above 10C.","1":"Leaf development - successive leaves emerge (V-stages).","3":"Stem elongation - rapid vegetative growth toward tasseling.","5":"Tasseling - the tassel fully emerges above the canopy.","6":"Flowering (silking/pollination) - most water-sensitive stage, avoid any drought stress.","7":"Development of fruit - kernels fill with a milky liquid.","8":"Ripening - kernels progress through dough, dent, and physiological maturity (black layer).","9":"Senescence - plant dries down, kernels hard, ready for harvest."}'),
            ('Corn - FAO 700', 'Late-maturing maize hybrid, roughly 135-145 days total cycle.', 'FAO 700', '{"0":"Germination - kernel absorbs water, radicle and coleoptile emerge; needs soil above 10C.","1":"Leaf development - successive leaves emerge (V-stages).","3":"Stem elongation - rapid vegetative growth toward tasseling.","5":"Tasseling - the tassel fully emerges above the canopy.","6":"Flowering (silking/pollination) - most water-sensitive stage, avoid any drought stress.","7":"Development of fruit - kernels fill with a milky liquid.","8":"Ripening - kernels progress through dough, dent, and physiological maturity (black layer).","9":"Senescence - plant dries down, kernels hard, ready for harvest."}');

            INSERT INTO "horticultureCatalogCropGrowthStage" ("HorticultureCatalogCropID", "StageNumber", "AirTempMin", "AirTempMax", "SoilTempMin", "SoilTempMax", "AirHumidityMin", "AirHumidityMax", "SoilMoistureMin", "SoilMoistureMax", "DurationDaysMin", "DurationDaysMax")
            SELECT "ID", 0, 4, 25, 4, 20, 50, 80, 60, 70, 10, 20 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (Bc Anica)'
            UNION ALL SELECT "ID", 1, 10, 20, 8, 18, 50, 75, 55, 65, 20, 30 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (Bc Anica)'
            UNION ALL SELECT "ID", 2, 15, 20, 10, 18, 50, 80, 55, 60, 90, 120 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (Bc Anica)'
            UNION ALL SELECT "ID", 3, 15, 20, 12, 18, 50, 75, 65, 70, 25, 30 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (Bc Anica)'
            UNION ALL SELECT "ID", 4, 15, 22, NULL, NULL, 45, 70, 68, 75, 7, 10 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (Bc Anica)'
            UNION ALL SELECT "ID", 5, 15, 25, NULL, NULL, 45, 70, 70, 78, 5, 8 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (Bc Anica)'
            UNION ALL SELECT "ID", 6, 15, 25, NULL, NULL, 40, 70, 75, 80, 5, 7 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (Bc Anica)'
            UNION ALL SELECT "ID", 7, 15, 25, NULL, NULL, 40, 70, 72, 80, 10, 12 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (Bc Anica)'
            UNION ALL SELECT "ID", 8, 18, 27, NULL, NULL, 35, 60, 55, 70, 10, 12 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (Bc Anica)'
            UNION ALL SELECT "ID", 9, 20, 30, NULL, NULL, 30, 55, 35, 55, 10, 15 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (Bc Anica)'

            UNION ALL SELECT "ID", 0, 4, 25, 4, 20, 50, 80, 60, 70, 10, 20 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (SJ. Tena)'
            UNION ALL SELECT "ID", 1, 10, 20, 8, 18, 50, 75, 55, 65, 20, 30 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (SJ. Tena)'
            UNION ALL SELECT "ID", 2, 15, 20, 10, 18, 50, 80, 55, 60, 90, 120 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (SJ. Tena)'
            UNION ALL SELECT "ID", 3, 15, 20, 12, 18, 50, 75, 65, 70, 25, 30 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (SJ. Tena)'
            UNION ALL SELECT "ID", 4, 15, 22, NULL, NULL, 45, 70, 68, 75, 7, 10 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (SJ. Tena)'
            UNION ALL SELECT "ID", 5, 15, 25, NULL, NULL, 45, 70, 70, 78, 5, 8 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (SJ. Tena)'
            UNION ALL SELECT "ID", 6, 15, 25, NULL, NULL, 40, 70, 75, 80, 5, 7 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (SJ. Tena)'
            UNION ALL SELECT "ID", 7, 15, 25, NULL, NULL, 40, 70, 72, 80, 10, 12 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (SJ. Tena)'
            UNION ALL SELECT "ID", 8, 18, 27, NULL, NULL, 35, 60, 55, 70, 10, 12 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (SJ. Tena)'
            UNION ALL SELECT "ID", 9, 20, 30, NULL, NULL, 30, 55, 35, 55, 10, 15 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Winter (SJ. Tena)'

            UNION ALL SELECT "ID", 0, 4, 25, 4, 20, 50, 70, 55, 65, 6, 10 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Spring'
            UNION ALL SELECT "ID", 1, 10, 22, 8, 20, 40, 70, 50, 60, 10, 15 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Spring'
            UNION ALL SELECT "ID", 2, 15, 22, 10, 20, 40, 75, 50, 60, 15, 20 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Spring'
            UNION ALL SELECT "ID", 3, 15, 24, 12, 20, 40, 70, 60, 68, 12, 15 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Spring'
            UNION ALL SELECT "ID", 4, 15, 25, NULL, NULL, 35, 65, 62, 72, 5, 7 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Spring'
            UNION ALL SELECT "ID", 5, 15, 27, NULL, NULL, 35, 65, 65, 75, 4, 6 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Spring'
            UNION ALL SELECT "ID", 6, 15, 28, NULL, NULL, 35, 65, 68, 75, 4, 6 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Spring'
            UNION ALL SELECT "ID", 7, 16, 28, NULL, NULL, 35, 65, 65, 75, 7, 9 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Spring'
            UNION ALL SELECT "ID", 8, 18, 30, NULL, NULL, 30, 55, 50, 65, 7, 9 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Spring'
            UNION ALL SELECT "ID", 9, 20, 32, NULL, NULL, 25, 50, 30, 50, 8, 10 FROM "horticultureCatalogCrop" WHERE "Name" = 'Wheat - Spring'

            UNION ALL SELECT "ID", 0, 18, 25, 15, 30, 45, 70, 60, 70, 4, 8 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 300'
            UNION ALL SELECT "ID", 1, 18, 27, 15, 25, 45, 70, 55, 65, 22, 30 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 300'
            UNION ALL SELECT "ID", 3, 20, 30, NULL, NULL, 45, 75, 65, 75, 15, 19 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 300'
            UNION ALL SELECT "ID", 5, 20, 30, NULL, NULL, 45, 75, 70, 80, 4, 6 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 300'
            UNION ALL SELECT "ID", 6, 20, 28, NULL, NULL, 50, 80, 75, 85, 4, 6 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 300'
            UNION ALL SELECT "ID", 7, 20, 28, NULL, NULL, 45, 75, 70, 80, 15, 19 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 300'
            UNION ALL SELECT "ID", 8, 18, 28, NULL, NULL, 35, 60, 40, 60, 18, 22 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 300'
            UNION ALL SELECT "ID", 9, 18, 30, NULL, NULL, 25, 50, 25, 40, 4, 8 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 300'

            UNION ALL SELECT "ID", 0, 18, 25, 15, 30, 45, 70, 60, 70, 5, 10 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 500'
            UNION ALL SELECT "ID", 1, 18, 27, 15, 25, 45, 70, 55, 65, 30, 40 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 500'
            UNION ALL SELECT "ID", 3, 20, 30, NULL, NULL, 45, 75, 65, 75, 20, 25 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 500'
            UNION ALL SELECT "ID", 5, 20, 30, NULL, NULL, 45, 75, 70, 80, 5, 8 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 500'
            UNION ALL SELECT "ID", 6, 20, 28, NULL, NULL, 50, 80, 75, 85, 5, 8 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 500'
            UNION ALL SELECT "ID", 7, 20, 28, NULL, NULL, 45, 75, 70, 80, 20, 25 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 500'
            UNION ALL SELECT "ID", 8, 18, 28, NULL, NULL, 35, 60, 40, 60, 25, 30 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 500'
            UNION ALL SELECT "ID", 9, 18, 30, NULL, NULL, 25, 50, 25, 40, 5, 10 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 500'

            UNION ALL SELECT "ID", 0, 18, 25, 15, 30, 45, 70, 60, 70, 6, 12 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 700'
            UNION ALL SELECT "ID", 1, 18, 27, 15, 25, 45, 70, 55, 65, 36, 48 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 700'
            UNION ALL SELECT "ID", 3, 20, 30, NULL, NULL, 45, 75, 65, 75, 24, 30 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 700'
            UNION ALL SELECT "ID", 5, 20, 30, NULL, NULL, 45, 75, 70, 80, 6, 10 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 700'
            UNION ALL SELECT "ID", 6, 20, 28, NULL, NULL, 50, 80, 75, 85, 6, 10 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 700'
            UNION ALL SELECT "ID", 7, 20, 28, NULL, NULL, 45, 75, 70, 80, 24, 30 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 700'
            UNION ALL SELECT "ID", 8, 18, 28, NULL, NULL, 35, 60, 40, 60, 30, 36 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 700'
            UNION ALL SELECT "ID", 9, 18, 30, NULL, NULL, 25, 50, 25, 40, 6, 12 FROM "horticultureCatalogCrop" WHERE "Name" = 'Corn - FAO 700';
            """;

        private const string SeedNames = "'Wheat - Winter (Bc Anica)', 'Wheat - Winter (SJ. Tena)', 'Wheat - Spring', 'Corn - FAO 300', 'Corn - FAO 500', 'Corn - FAO 700'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClassCode",
                table: "horticultureCatalogCrop",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhaseDescriptionsJson",
                table: "horticultureCatalogCrop",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "horticultureCatalogCropGrowthStage",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HorticultureCatalogCropID = table.Column<int>(type: "integer", nullable: false),
                    StageNumber = table.Column<int>(type: "integer", nullable: false),
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
                    DurationDaysMin = table.Column<int>(type: "integer", nullable: true),
                    DurationDaysMax = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_horticultureCatalogCropGrowthStage", x => x.ID);
                    table.ForeignKey(
                        name: "FK_horticultureCatalogCropGrowthStage_horticultureCatalogCrop_~",
                        column: x => x.HorticultureCatalogCropID,
                        principalTable: "horticultureCatalogCrop",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_horticultureCatalogCropGrowthStage_crop_stage",
                table: "horticultureCatalogCropGrowthStage",
                columns: new[] { "HorticultureCatalogCropID", "StageNumber" },
                unique: true);

            migrationBuilder.Sql(SeedSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DELETE FROM \"horticultureCatalogCrop\" WHERE \"Name\" IN ({SeedNames});");

            migrationBuilder.DropTable(
                name: "horticultureCatalogCropGrowthStage");

            migrationBuilder.DropColumn(
                name: "ClassCode",
                table: "horticultureCatalogCrop");

            migrationBuilder.DropColumn(
                name: "PhaseDescriptionsJson",
                table: "horticultureCatalogCrop");
        }
    }
}
