using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceDiagnosticHeapAndSchemaColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConfigSchemaVersion",
                table: "deviceDiagnostic",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaxAllocHeapBytes",
                table: "deviceDiagnostic",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MinFreeHeapBytes",
                table: "deviceDiagnostic",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StackHighWaterMarkBytes",
                table: "deviceDiagnostic",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfigSchemaVersion",
                table: "deviceDiagnostic");

            migrationBuilder.DropColumn(
                name: "MaxAllocHeapBytes",
                table: "deviceDiagnostic");

            migrationBuilder.DropColumn(
                name: "MinFreeHeapBytes",
                table: "deviceDiagnostic");

            migrationBuilder.DropColumn(
                name: "StackHighWaterMarkBytes",
                table: "deviceDiagnostic");
        }
    }
}
