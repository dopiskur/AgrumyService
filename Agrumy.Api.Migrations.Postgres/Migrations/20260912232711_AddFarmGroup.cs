using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
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
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "farmGroup",
                columns: table => new
                {
                    IDFarmGroup = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmGroup", x => x.IDFarmGroup);
                });

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
