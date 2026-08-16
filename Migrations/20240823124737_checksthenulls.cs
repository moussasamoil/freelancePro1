using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class checksthenulls : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePaymentSummary_Employees_EmployeeId",
                table: "EmployeePaymentSummary");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeTransactions_EmployeePaymentSummary_EmployeePaymentSummaryId",
                table: "EmployeeTransactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EmployeePaymentSummary",
                table: "EmployeePaymentSummary");

            migrationBuilder.RenameTable(
                name: "EmployeePaymentSummary",
                newName: "EmployeePaymentSummaries");

            migrationBuilder.RenameIndex(
                name: "IX_EmployeePaymentSummary_EmployeeId",
                table: "EmployeePaymentSummaries",
                newName: "IX_EmployeePaymentSummaries_EmployeeId");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCommissions",
                table: "EmployeePaymentSummaries",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddPrimaryKey(
                name: "PK_EmployeePaymentSummaries",
                table: "EmployeePaymentSummaries",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePaymentSummaries_Employees_EmployeeId",
                table: "EmployeePaymentSummaries",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_EmployeePaymentSummaries_Employees_EmployeeId",
                table: "EmployeePaymentSummaries");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeTransactions_EmployeePaymentSummaries_EmployeePaymentSummaryId",
                table: "EmployeeTransactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EmployeePaymentSummaries",
                table: "EmployeePaymentSummaries");

            migrationBuilder.DropColumn(
                name: "TotalCommissions",
                table: "EmployeePaymentSummaries");

            migrationBuilder.RenameTable(
                name: "EmployeePaymentSummaries",
                newName: "EmployeePaymentSummary");

            migrationBuilder.RenameIndex(
                name: "IX_EmployeePaymentSummaries_EmployeeId",
                table: "EmployeePaymentSummary",
                newName: "IX_EmployeePaymentSummary_EmployeeId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EmployeePaymentSummary",
                table: "EmployeePaymentSummary",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeePaymentSummary_Employees_EmployeeId",
                table: "EmployeePaymentSummary",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeTransactions_EmployeePaymentSummary_EmployeePaymentSummaryId",
                table: "EmployeeTransactions",
                column: "EmployeePaymentSummaryId",
                principalTable: "EmployeePaymentSummary",
                principalColumn: "Id");
        }
    }
}
