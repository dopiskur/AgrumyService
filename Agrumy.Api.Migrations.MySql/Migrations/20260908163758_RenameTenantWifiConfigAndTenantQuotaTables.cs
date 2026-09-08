using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class RenameTenantWifiConfigAndTenantQuotaTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenantQuota_tenant_IDTenant",
                table: "tenantQuota");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tenantWifiConfig",
                table: "tenantWifiConfig");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tenantQuota",
                table: "tenantQuota");

            migrationBuilder.RenameTable(
                name: "tenantWifiConfig",
                newName: "tenantConfigWifi");

            migrationBuilder.RenameTable(
                name: "tenantQuota",
                newName: "tenantConfigQuota");

            migrationBuilder.RenameIndex(
                name: "ix_tenantWifiConfig_tenant",
                table: "tenantConfigWifi",
                newName: "ix_tenantConfigWifi_tenant");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tenantConfigWifi",
                table: "tenantConfigWifi",
                column: "IDTenantWifiConfig");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tenantConfigQuota",
                table: "tenantConfigQuota",
                column: "IDTenant");

            migrationBuilder.AddForeignKey(
                name: "FK_tenantConfigQuota_tenant_IDTenant",
                table: "tenantConfigQuota",
                column: "IDTenant",
                principalTable: "tenant",
                principalColumn: "IDTenant",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenantConfigQuota_tenant_IDTenant",
                table: "tenantConfigQuota");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tenantConfigWifi",
                table: "tenantConfigWifi");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tenantConfigQuota",
                table: "tenantConfigQuota");

            migrationBuilder.RenameTable(
                name: "tenantConfigWifi",
                newName: "tenantWifiConfig");

            migrationBuilder.RenameTable(
                name: "tenantConfigQuota",
                newName: "tenantQuota");

            migrationBuilder.RenameIndex(
                name: "ix_tenantConfigWifi_tenant",
                table: "tenantWifiConfig",
                newName: "ix_tenantWifiConfig_tenant");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tenantWifiConfig",
                table: "tenantWifiConfig",
                column: "IDTenantWifiConfig");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tenantQuota",
                table: "tenantQuota",
                column: "IDTenant");

            migrationBuilder.AddForeignKey(
                name: "FK_tenantQuota_tenant_IDTenant",
                table: "tenantQuota",
                column: "IDTenant",
                principalTable: "tenant",
                principalColumn: "IDTenant",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
