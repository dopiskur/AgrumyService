using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FarmGroupID",
                table: "farm",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "farmGroup",
                columns: table => new
                {
                    IDFarmGroup = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TenantID = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Deleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmGroup", x => x.IDFarmGroup);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_farm_FarmGroupID",
                table: "farm",
                column: "FarmGroupID");

            migrationBuilder.CreateIndex(
                name: "ix_farmGroup_tenant",
                table: "farmGroup",
                column: "TenantID");

            migrationBuilder.AddForeignKey(
                name: "FK_farm_farmGroup_FarmGroupID",
                table: "farm",
                column: "FarmGroupID",
                principalTable: "farmGroup",
                principalColumn: "IDFarmGroup",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_farm_farmGroup_FarmGroupID",
                table: "farm");

            migrationBuilder.DropTable(
                name: "farmGroup");

            migrationBuilder.DropIndex(
                name: "IX_farm_FarmGroupID",
                table: "farm");

            migrationBuilder.DropColumn(
                name: "FarmGroupID",
                table: "farm");
        }
    }
}
