using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class RenameTenantQuotaParcelsAddSowingsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MaxParcels always counted zones already (see TenantQuotaEnforcer's own history), just under the wrong name - a real rename, not a drop+add.
            migrationBuilder.RenameColumn(
                name: "MaxParcels",
                table: "tenantConfigQuota",
                newName: "MaxFarmParcelZones");

            migrationBuilder.AddColumn<int>(
                name: "MaxSowingsActive",
                table: "tenantConfigQuota",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxSowingsActive",
                table: "tenantConfigQuota");

            migrationBuilder.RenameColumn(
                name: "MaxFarmParcelZones",
                table: "tenantConfigQuota",
                newName: "MaxParcels");
        }
    }
}
