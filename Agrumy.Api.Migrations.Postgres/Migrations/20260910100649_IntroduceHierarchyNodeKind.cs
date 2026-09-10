using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class IntroduceHierarchyNodeKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxCrops",
                table: "tenantConfigQuota",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxParcels",
                table: "tenantConfigQuota",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FarmType",
                table: "farm",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // simulationGroup.Scope was Unit=1/Zone=2 (Agrumy.Shared.Models.SimulationGroupScope, now removed); HierarchyNodeKind's Unit=2/Zone=3 - remap existing rows so old data still resolves to the same scope.
            migrationBuilder.Sql("UPDATE \"simulationGroup\" SET \"Scope\" = \"Scope\" + 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"simulationGroup\" SET \"Scope\" = \"Scope\" - 1;");

            migrationBuilder.DropColumn(
                name: "MaxCrops",
                table: "tenantConfigQuota");

            migrationBuilder.DropColumn(
                name: "MaxParcels",
                table: "tenantConfigQuota");

            migrationBuilder.DropColumn(
                name: "FarmType",
                table: "farm");
        }
    }
}
