using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyStoredProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op on Postgres: the pre-EF-Core SqlRepository/ADO.NET procedures this migration drops were MySQL-only, Postgres never had them.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
