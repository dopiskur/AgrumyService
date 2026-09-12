using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class ReorderServerConfigTenantColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Purely cosmetic physical reorder (no model/type change) - MySQL supports MODIFY COLUMN ... AFTER directly, unlike Postgres (see the sibling migration there).
            migrationBuilder.Sql("ALTER TABLE `serverConfig` MODIFY COLUMN `AllowSelfServiceTenantCreation` tinyint(1) NULL AFTER `PortHTTPS`;");
            migrationBuilder.Sql("ALTER TABLE `serverConfig` MODIFY COLUMN `TenantManagementEnabled` tinyint(1) NOT NULL DEFAULT 0 AFTER `AllowSelfServiceTenantCreation`;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `serverConfig` MODIFY COLUMN `TenantManagementEnabled` tinyint(1) NOT NULL DEFAULT 0 AFTER `MaxRulesPerZone`;");
            migrationBuilder.Sql("ALTER TABLE `serverConfig` MODIFY COLUMN `AllowSelfServiceTenantCreation` tinyint(1) NULL AFTER `ActivationResendCooldownMinutes`;");
        }
    }
}
