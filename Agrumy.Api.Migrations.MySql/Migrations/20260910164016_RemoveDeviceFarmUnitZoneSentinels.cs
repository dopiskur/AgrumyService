using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeviceFarmUnitZoneSentinels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drops the legacy IDFarmGreenhouseUnit=0/IDFarmGreenhouseUnitZone=0 "magic zero" sentinel pair - nothing references them any more. Zone first - its FK points at the Unit row.
            migrationBuilder.Sql("DELETE FROM farmGreenhouseUnitZone WHERE IDFarmGreenhouseUnitZone = 0;");
            migrationBuilder.Sql("DELETE FROM farmGreenhouseUnit WHERE IDFarmGreenhouseUnit = 0;");

            migrationBuilder.AlterColumn<int>(
                name: "IDFarmGreenhouseUnitZone",
                table: "farmGreenhouseUnitZone",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "IDFarmGreenhouseUnit",
                table: "farmGreenhouseUnit",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "IDFarmGreenhouseUnitZone",
                table: "farmGreenhouseUnitZone",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<int>(
                name: "IDFarmGreenhouseUnit",
                table: "farmGreenhouseUnit",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);
        }
    }
}
