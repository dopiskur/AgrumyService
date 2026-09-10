using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSsrfAllowlists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "firmwareSsrfAllowlistEntry",
                columns: table => new
                {
                    IDFirmwareSsrfAllowlistEntry = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Pattern = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AllowPrivateNetwork = table.Column<bool>(type: "boolean", nullable: false),
                    AllowInsecureHttp = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_firmwareSsrfAllowlistEntry", x => x.IDFirmwareSsrfAllowlistEntry);
                });

            migrationBuilder.CreateTable(
                name: "webhookSsrfAllowlistEntry",
                columns: table => new
                {
                    IDWebhookSsrfAllowlistEntry = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Pattern = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AllowPrivateNetwork = table.Column<bool>(type: "boolean", nullable: false),
                    AllowInsecureHttp = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhookSsrfAllowlistEntry", x => x.IDWebhookSsrfAllowlistEntry);
                });

            migrationBuilder.CreateIndex(
                name: "ux_firmwareSsrfAllowlistEntry_pattern",
                table: "firmwareSsrfAllowlistEntry",
                column: "Pattern",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_webhookSsrfAllowlistEntry_pattern",
                table: "webhookSsrfAllowlistEntry",
                column: "Pattern",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "firmwareSsrfAllowlistEntry");

            migrationBuilder.DropTable(
                name: "webhookSsrfAllowlistEntry");
        }
    }
}
