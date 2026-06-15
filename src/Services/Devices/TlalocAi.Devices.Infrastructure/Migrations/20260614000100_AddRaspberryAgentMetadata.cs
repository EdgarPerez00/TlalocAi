using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TlalocAi.Devices.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DevicesDbContext))]
    [Migration("20260614000100_AddRaspberryAgentMetadata")]
    public partial class AddRaspberryAgentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ObservedPublicIpAddress",
                table: "devices_devices",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Hostname",
                table: "devices_devices",
                type: "varchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentVersion",
                table: "devices_devices",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ObservedPublicIpAddress", table: "devices_devices");
            migrationBuilder.DropColumn(name: "Hostname", table: "devices_devices");
            migrationBuilder.DropColumn(name: "AgentVersion", table: "devices_devices");
        }
    }
}
