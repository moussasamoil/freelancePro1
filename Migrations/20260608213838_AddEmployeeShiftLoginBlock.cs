using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddEmployeeShiftLoginBlock : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLoginBlocked",
                table: "EmployeeWorkShifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LoginBlockedAt",
                table: "EmployeeWorkShifts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoginBlockReason",
                table: "EmployeeWorkShifts",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdminUnblockedUntil",
                table: "EmployeeWorkShifts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdminUnblockedAt",
                table: "EmployeeWorkShifts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminUnblockedByUserId",
                table: "EmployeeWorkShifts",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLoginBlocked",
                table: "EmployeeWorkShifts");

            migrationBuilder.DropColumn(
                name: "LoginBlockedAt",
                table: "EmployeeWorkShifts");

            migrationBuilder.DropColumn(
                name: "LoginBlockReason",
                table: "EmployeeWorkShifts");

            migrationBuilder.DropColumn(
                name: "AdminUnblockedUntil",
                table: "EmployeeWorkShifts");

            migrationBuilder.DropColumn(
                name: "AdminUnblockedAt",
                table: "EmployeeWorkShifts");

            migrationBuilder.DropColumn(
                name: "AdminUnblockedByUserId",
                table: "EmployeeWorkShifts");
        }
    }
}