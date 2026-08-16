using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class TightenEmployeeBonusIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmployeeBonusRates_EmployeeId",
                table: "EmployeeBonusRates");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeBonusPayments_EmployeeId",
                table: "EmployeeBonusPayments");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBonusRates_EmployeeId",
                table: "EmployeeBonusRates",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBonusPayments_EmployeeId_DatePaid",
                table: "EmployeeBonusPayments",
                columns: new[] { "EmployeeId", "DatePaid" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmployeeBonusRates_EmployeeId",
                table: "EmployeeBonusRates");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeBonusPayments_EmployeeId_DatePaid",
                table: "EmployeeBonusPayments");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBonusRates_EmployeeId",
                table: "EmployeeBonusRates",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBonusPayments_EmployeeId",
                table: "EmployeeBonusPayments",
                column: "EmployeeId");
        }
    }
}
