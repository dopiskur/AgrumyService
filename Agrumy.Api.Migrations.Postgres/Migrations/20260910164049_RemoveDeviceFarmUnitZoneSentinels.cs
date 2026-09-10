using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeviceFarmUnitZoneSentinels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drops the legacy IDFarmGreenhouseUnit=0/IDFarmGreenhouseUnitZone=0 "magic zero" sentinel pair - nothing references them any more. Zone first - its FK points at the Unit row.
            migrationBuilder.Sql("DELETE FROM \"farmGreenhouseUnitZone\" WHERE \"IDFarmGreenhouseUnitZone\" = 0;");
            migrationBuilder.Sql("DELETE FROM \"farmGreenhouseUnit\" WHERE \"IDFarmGreenhouseUnit\" = 0;");

            migrationBuilder.AlterColumn<int>(
                name: "IDFarmGreenhouseUnitZone",
                table: "farmGreenhouseUnitZone",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "IDFarmGreenhouseUnit",
                table: "farmGreenhouseUnit",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            // ADD GENERATED ... AS IDENTITY creates the sequence starting at 1 regardless of rows already present - without this, the next real insert collides with an existing manually-assigned id.
            migrationBuilder.Sql("SELECT setval(pg_get_serial_sequence('\"farmGreenhouseUnit\"', 'IDFarmGreenhouseUnit'), COALESCE((SELECT MAX(\"IDFarmGreenhouseUnit\") FROM \"farmGreenhouseUnit\"), 0) + 1, false);");
            migrationBuilder.Sql("SELECT setval(pg_get_serial_sequence('\"farmGreenhouseUnitZone\"', 'IDFarmGreenhouseUnitZone'), COALESCE((SELECT MAX(\"IDFarmGreenhouseUnitZone\") FROM \"farmGreenhouseUnitZone\"), 0) + 1, false);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "IDFarmGreenhouseUnitZone",
                table: "farmGreenhouseUnitZone",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "IDFarmGreenhouseUnit",
                table: "farmGreenhouseUnit",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }
    }
}
