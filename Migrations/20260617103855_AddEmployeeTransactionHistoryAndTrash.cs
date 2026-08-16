using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddEmployeeTransactionHistoryAndTrash : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "EmployeeTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserName",
                table: "EmployeeTransactions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditHistoryJson",
                table: "EmployeeTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "EmployeeTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "EmployeeTransactions");

            migrationBuilder.DropColumn(
                name: "DeletedByUserName",
                table: "EmployeeTransactions");

            migrationBuilder.DropColumn(
                name: "EditHistoryJson",
                table: "EmployeeTransactions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "EmployeeTransactions");
        }
    }
}
