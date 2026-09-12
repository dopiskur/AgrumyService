using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmParcelGroupCrop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "farmParcelGroupCrop",
                columns: table => new
                {
                    IDFarmParcelGroupCrop = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TenantID = table.Column<int>(type: "int", nullable: true),
                    FarmID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmParcelGroupCrop", x => x.IDFarmParcelGroupCrop);
                    table.ForeignKey(
                        name: "FK_farmParcelGroupCrop_farm_FarmID",
                        column: x => x.FarmID,
                        principalTable: "farm",
                        principalColumn: "IDFarm");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "farmParcelGroupCropMember",
                columns: table => new
                {
                    FarmParcelGroupCropID = table.Column<int>(type: "int", nullable: false),
                    FarmParcelID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmParcelGroupCropMember", x => new { x.FarmParcelGroupCropID, x.FarmParcelID });
                    table.ForeignKey(
                        name: "FK_farmParcelGroupCropMember_farmParcelGroupCrop_FarmParcelGrou~",
                        column: x => x.FarmParcelGroupCropID,
                        principalTable: "farmParcelGroupCrop",
                        principalColumn: "IDFarmParcelGroupCrop",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_farmParcelGroupCropMember_farmParcel_FarmParcelID",
                        column: x => x.FarmParcelID,
                        principalTable: "farmParcel",
                        principalColumn: "IDFarmParcel",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_farmParcelGroupCrop_FarmID",
                table: "farmParcelGroupCrop",
                column: "FarmID");

            migrationBuilder.CreateIndex(
                name: "IX_farmParcelGroupCropMember_FarmParcelID",
                table: "farmParcelGroupCropMember",
                column: "FarmParcelID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "farmParcelGroupCropMember");

            migrationBuilder.DropTable(
                name: "farmParcelGroupCrop");
        }
    }
}
