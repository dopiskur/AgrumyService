using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenantQuota",
                columns: table => new
                {
                    IDTenant = table.Column<int>(type: "integer", nullable: false),
                    MaxFarms = table.Column<int>(type: "integer", nullable: false),
                    MaxUnits = table.Column<int>(type: "integer", nullable: false),
                    MaxZones = table.Column<int>(type: "integer", nullable: false),
                    MaxControllersPerDevice = table.Column<int>(type: "integer", nullable: false),
                    MaxSensorsPerDevice = table.Column<int>(type: "integer", nullable: false),
                    MqttEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LoRaEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    GatewayEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MinSensorIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxDataRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    RecycleBinRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false),
                    MaxSimulations = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenantQuota", x => x.IDTenant);
                    table.ForeignKey(
                        name: "FK_tenantQuota_tenant_IDTenant",
                        column: x => x.IDTenant,
                        principalTable: "tenant",
                        principalColumn: "IDTenant",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenantQuota");
        }
    }
}
