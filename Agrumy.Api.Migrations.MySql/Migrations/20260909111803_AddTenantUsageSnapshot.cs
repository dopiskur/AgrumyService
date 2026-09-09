using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantUsageSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenantUsageSnapshot",
                columns: table => new
                {
                    IDTenantUsageSnapshot = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TenantID = table.Column<int>(type: "int", nullable: false),
                    SnapshotDateUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    DeviceCount = table.Column<int>(type: "int", nullable: false),
                    SensorDataRowCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenantUsageSnapshot", x => x.IDTenantUsageSnapshot);
                    table.ForeignKey(
                        name: "FK_tenantUsageSnapshot_tenant_TenantID",
                        column: x => x.TenantID,
                        principalTable: "tenant",
                        principalColumn: "IDTenant",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_tenantUsageSnapshot_tenant_date",
                table: "tenantUsageSnapshot",
                columns: new[] { "TenantID", "SnapshotDateUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenantUsageSnapshot");
        }
    }
}
