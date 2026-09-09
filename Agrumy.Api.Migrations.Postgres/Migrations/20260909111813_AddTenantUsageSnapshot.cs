using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
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
                    IDTenantUsageSnapshot = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    SnapshotDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeviceCount = table.Column<int>(type: "integer", nullable: false),
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
                });

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
