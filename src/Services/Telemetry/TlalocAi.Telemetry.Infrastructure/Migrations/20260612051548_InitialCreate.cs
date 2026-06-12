using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TlalocAi.Telemetry.Infrastructure.Migrations
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
                name: "telemetry_experiments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    DeviceId = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_experiments", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "telemetry_measurements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    DeviceId = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    ExperimentId = table.Column<Guid>(type: "char(36)", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FlowLpm = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    TotalLiters = table.Column<decimal>(type: "decimal(14,4)", precision: 14, scale: 4, nullable: false),
                    PumpOn = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_measurements", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "telemetry_actuator_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    MeasurementId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ActuatorName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    IsOn = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_actuator_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_telemetry_actuator_snapshots_telemetry_measurements_Measurem~",
                        column: x => x.MeasurementId,
                        principalTable: "telemetry_measurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "telemetry_level_measurements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    MeasurementId = table.Column<Guid>(type: "char(36)", nullable: false),
                    SensorName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_level_measurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_telemetry_level_measurements_telemetry_measurements_Measurem~",
                        column: x => x.MeasurementId,
                        principalTable: "telemetry_measurements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_actuator_snapshots_MeasurementId",
                table: "telemetry_actuator_snapshots",
                column: "MeasurementId");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_level_measurements_MeasurementId",
                table: "telemetry_level_measurements",
                column: "MeasurementId");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_measurements_DeviceId_TimestampUtc",
                table: "telemetry_measurements",
                columns: new[] { "DeviceId", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telemetry_actuator_snapshots");

            migrationBuilder.DropTable(
                name: "telemetry_experiments");

            migrationBuilder.DropTable(
                name: "telemetry_level_measurements");

            migrationBuilder.DropTable(
                name: "telemetry_measurements");
        }
    }
}
