using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TlalocAi.Devices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "devices_devices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ApiKeyHash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices_devices", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "devices_actuators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    DeviceId = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    GpioPin = table.Column<int>(type: "int", nullable: false),
                    ActiveLow = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices_actuators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_devices_actuators_devices_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "devices_sensors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    DeviceId = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    GpioPin = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices_sensors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_devices_sensors_devices_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices_devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_devices_actuators_DeviceId_Name",
                table: "devices_actuators",
                columns: new[] { "DeviceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_sensors_DeviceId_Name",
                table: "devices_sensors",
                columns: new[] { "DeviceId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "devices_actuators");

            migrationBuilder.DropTable(
                name: "devices_sensors");

            migrationBuilder.DropTable(
                name: "devices_devices");
        }
    }
}
