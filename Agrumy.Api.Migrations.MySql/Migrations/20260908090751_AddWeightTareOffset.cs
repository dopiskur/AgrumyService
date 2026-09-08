using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddWeightTareOffset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "WeightTareOffset",
                table: "deviceConfigSensor",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeightTareOffset",
                table: "deviceConfigSensor");
        }
    }
}
