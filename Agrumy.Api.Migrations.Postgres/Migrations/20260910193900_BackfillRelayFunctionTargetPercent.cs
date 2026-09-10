using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class BackfillRelayFunctionTargetPercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TargetPercent is now required for every Relay rule (ActionType 1), not just Screen/Vent - existing rows get 100 so the fold's behavior is unchanged (percent > 0 still equals the old on/off outcome).
            migrationBuilder.Sql("UPDATE \"deviceFarmUnitZoneRule\" SET \"TargetPercent\" = 100 WHERE \"ActionType\" = 1 AND \"TargetPercent\" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration - no reliable way to tell a genuinely-authored 100 apart from a backfilled one, so Down is a no-op rather than reintroducing NULLs that would now fail validation.
        }
    }
}
