using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class HashDeviceSessionToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DeviceSessionHandler now compares a SHA-256 hash, not the raw token - every existing plaintext
            // value here is permanently unusable against that check (can't be hashed retroactively into a match),
            // so clear it rather than leave a dead plaintext secret sitting in the table. A device with an
            // active session just gets one forced re-Authenticate on its next poll, same self-healing 401 retry
            // it already does after a normal session expiry.
            migrationBuilder.Sql("UPDATE `device` SET `ApiAuthToken` = NULL, `ApiAuthExpiresAtUtc` = NULL WHERE `ApiAuthToken` IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No plaintext to restore - Down clears too, same as Up, rather than pretending a rollback can recover it.
            migrationBuilder.Sql("UPDATE `device` SET `ApiAuthToken` = NULL, `ApiAuthExpiresAtUtc` = NULL WHERE `ApiAuthToken` IS NOT NULL;");
        }
    }
}
