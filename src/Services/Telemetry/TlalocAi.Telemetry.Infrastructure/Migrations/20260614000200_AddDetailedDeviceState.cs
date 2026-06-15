using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TlalocAi.Telemetry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDetailedDeviceState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetailedStateJson",
                table: "telemetry_measurements",
                type: "json",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DetailedStateJson", table: "telemetry_measurements");
        }
    }
}
