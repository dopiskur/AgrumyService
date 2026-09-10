using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
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
                    IDFirmwareSsrfAllowlistEntry = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Pattern = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AllowPrivateNetwork = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AllowInsecureHttp = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_firmwareSsrfAllowlistEntry", x => x.IDFirmwareSsrfAllowlistEntry);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "webhookSsrfAllowlistEntry",
                columns: table => new
                {
                    IDWebhookSsrfAllowlistEntry = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Pattern = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AllowPrivateNetwork = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AllowInsecureHttp = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhookSsrfAllowlistEntry", x => x.IDWebhookSsrfAllowlistEntry);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
