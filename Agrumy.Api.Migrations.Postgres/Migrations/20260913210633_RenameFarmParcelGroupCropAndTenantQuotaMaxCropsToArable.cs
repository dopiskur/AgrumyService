using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RenameFarmParcelGroupCropAndTenantQuotaMaxCropsToArable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MaxCrops",
                table: "tenantConfigQuota",
                newName: "MaxArables");

            migrationBuilder.RenameTable(
                name: "farmParcelGroupCropMember",
                newName: "farmParcelGroupArableMember");

            migrationBuilder.RenameTable(
                name: "farmParcelGroupCrop",
                newName: "farmParcelGroupArable");

            migrationBuilder.RenameColumn(
                name: "FarmParcelGroupCropID",
                table: "farmParcelGroupArableMember",
                newName: "FarmParcelGroupArableID");

            migrationBuilder.RenameColumn(
                name: "IDFarmParcelGroupCrop",
                table: "farmParcelGroupArable",
                newName: "IDFarmParcelGroupArable");

            migrationBuilder.RenameIndex(
                name: "IX_farmParcelGroupCrop_FarmID",
                table: "farmParcelGroupArable",
                newName: "IX_farmParcelGroupArable_FarmID");

            migrationBuilder.RenameIndex(
                name: "IX_farmParcelGroupCropMember_FarmParcelID",
                table: "farmParcelGroupArableMember",
                newName: "IX_farmParcelGroupArableMember_FarmParcelID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_farmParcelGroupArableMember_FarmParcelID",
                table: "farmParcelGroupArableMember",
                newName: "IX_farmParcelGroupCropMember_FarmParcelID");

            migrationBuilder.RenameIndex(
                name: "IX_farmParcelGroupArable_FarmID",
                table: "farmParcelGroupArable",
                newName: "IX_farmParcelGroupCrop_FarmID");

            migrationBuilder.RenameColumn(
                name: "IDFarmParcelGroupArable",
                table: "farmParcelGroupArable",
                newName: "IDFarmParcelGroupCrop");

            migrationBuilder.RenameColumn(
                name: "FarmParcelGroupArableID",
                table: "farmParcelGroupArableMember",
                newName: "FarmParcelGroupCropID");

            migrationBuilder.RenameTable(
                name: "farmParcelGroupArable",
                newName: "farmParcelGroupCrop");

            migrationBuilder.RenameTable(
                name: "farmParcelGroupArableMember",
                newName: "farmParcelGroupCropMember");

            migrationBuilder.RenameColumn(
                name: "MaxArables",
                table: "tenantConfigQuota",
                newName: "MaxCrops");
        }
    }
}
