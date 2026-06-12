using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TlalocAi.Control.Infrastructure.Migrations
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
                name: "control_commands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    DeviceId = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    Type = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    Target = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    State = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExecutedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_control_commands", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_control_commands_DeviceId_Status",
                table: "control_commands",
                columns: new[] { "DeviceId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "control_commands");
        }
    }
}
