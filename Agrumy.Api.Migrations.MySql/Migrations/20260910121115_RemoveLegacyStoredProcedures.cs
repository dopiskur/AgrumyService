using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyStoredProcedures : Migration
    {
        private static readonly string[] LegacyProcedureNames =
        [
            "DeviceAdd", "DeviceCheckMacAddress", "DeviceConfigControllerGet", "DeviceConfigControllerUpdate",
            "DeviceConfigSensorGet", "DeviceConfigSensorUpdate", "DeviceDelete", "DeviceGet", "DevicesGet",
            "DeviceTypeGet", "DeviceTypeRelayGet", "DeviceTypeSensorGet", "DeviceTypeServiceGet", "DeviceUpdate",
            "SensorDataDelete", "SensorDataGet", "SensorDataGetJson", "SensorDataPush", "SensorDataReportBuilder",
            "SensorDataReportGet", "SensorDataReportTest", "ServerConfig", "ServerConfigAdd", "ServerConfigGet",
            "TenantAdd", "TenantGet", "UserAdd", "UserDelete", "UserGet", "UserGroupAdd", "UserGroupDelete",
            "UserGroupGet", "UserGroupsGet", "UserRoleGet", "UserSecretGet", "UserSetPassword", "UsersGet",
            "UserUpdate", "_JsonExample", "_sensorDataTest", "_testproc",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pre-EF-Core SqlRepository/ADO.NET era procedures (proc name = MethodBase.GetCurrentMethod().Name); confirmed zero callers in current C# code and never referenced by any EF migration.
            foreach (string name in LegacyProcedureNames)
            {
                migrationBuilder.Sql($"DROP PROCEDURE IF EXISTS `{name}`;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: nothing in the codebase calls these procedures, so recreating them on rollback restores nothing functional.
        }
    }
}
