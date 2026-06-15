using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TlalocAi.Control.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetType",
                table: "control_commands",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetId",
                table: "control_commands",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommandType",
                table: "control_commands",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedBy",
                table: "control_commands",
                type: "varchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Payload",
                table: "control_commands",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultMessage",
                table: "control_commands",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TargetType", table: "control_commands");
            migrationBuilder.DropColumn(name: "TargetId", table: "control_commands");
            migrationBuilder.DropColumn(name: "CommandType", table: "control_commands");
            migrationBuilder.DropColumn(name: "RequestedBy", table: "control_commands");
            migrationBuilder.DropColumn(name: "Payload", table: "control_commands");
            migrationBuilder.DropColumn(name: "ResultMessage", table: "control_commands");
        }
    }
}
