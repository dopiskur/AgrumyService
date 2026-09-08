using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
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
                    IDTenant = table.Column<int>(type: "int", nullable: false),
                    MaxFarms = table.Column<int>(type: "int", nullable: false),
                    MaxUnits = table.Column<int>(type: "int", nullable: false),
                    MaxZones = table.Column<int>(type: "int", nullable: false),
                    MaxControllersPerDevice = table.Column<int>(type: "int", nullable: false),
                    MaxSensorsPerDevice = table.Column<int>(type: "int", nullable: false),
                    MqttEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LoRaEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    GatewayEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MinSensorIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxDataRetentionDays = table.Column<int>(type: "int", nullable: false),
                    RecycleBinRetentionDays = table.Column<int>(type: "int", nullable: false),
                    MaxUsers = table.Column<int>(type: "int", nullable: false),
                    MaxSimulations = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenantQuota");
        }
    }
}
