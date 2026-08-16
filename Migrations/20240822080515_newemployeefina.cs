using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class newemployeefina : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmployeePaymentSummaryId",
                table: "EmployeeTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeePaymentSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Month = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OngoingAccount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalBonuses = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAdvances = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalSalaryPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePaymentSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePaymentSummaries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTransactions_EmployeePaymentSummaryId",
                table: "EmployeeTransactions",
                column: "EmployeePaymentSummaryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePaymentSummaries_EmployeeId",
                table: "EmployeePaymentSummaries",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeTransactions_EmployeePaymentSummaries_EmployeePaymentSummaryId",
                table: "EmployeeTransactions",
                column: "EmployeePaymentSummaryId",
                principalTable: "EmployeePaymentSummaries",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeTransactions_EmployeePaymentSummaries_EmployeePaymentSummaryId",
                table: "EmployeeTransactions");

            migrationBuilder.DropTable(
                name: "EmployeePaymentSummaries");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeTransactions_EmployeePaymentSummaryId",
                table: "EmployeeTransactions");

            migrationBuilder.DropColumn(
                name: "EmployeePaymentSummaryId",
                table: "EmployeeTransactions");
        }
    }
}
