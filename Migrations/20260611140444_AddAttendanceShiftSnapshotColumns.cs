using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddAttendanceShiftSnapshotColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ShiftEndAt",
                table: "EmployeeAttendanceLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShiftId",
                table: "EmployeeAttendanceLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShiftStartAt",
                table: "EmployeeAttendanceLogs",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShiftEndAt",
                table: "EmployeeAttendanceLogs");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "EmployeeAttendanceLogs");

            migrationBuilder.DropColumn(
                name: "ShiftStartAt",
                table: "EmployeeAttendanceLogs");
        }
    }
}
