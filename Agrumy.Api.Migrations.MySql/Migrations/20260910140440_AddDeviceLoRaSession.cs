using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceLoRaSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deviceLoRaSession",
                columns: table => new
                {
                    DeviceID = table.Column<int>(type: "int", nullable: false),
                    BootNonceHex = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MaxCounter = table.Column<long>(type: "bigint", nullable: false),
                    FirstSeenUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceLoRaSession", x => new { x.DeviceID, x.BootNonceHex });
                    table.ForeignKey(
                        name: "FK_deviceLoRaSession_device_DeviceID",
                        column: x => x.DeviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_deviceLoRaSession_device_firstseen",
                table: "deviceLoRaSession",
                columns: new[] { "DeviceID", "FirstSeenUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deviceLoRaSession");
        }
    }
}
