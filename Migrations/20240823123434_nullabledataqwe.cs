using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class nullabledataqwe : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePaymentSummaries_Employees_EmployeeId",
                table: "EmployeePaymentSummaries");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeTransactions_EmployeePaymentSummaries_EmployeePaymentSummaryId",
                table: "EmployeeTransactions");

            migrationBuilder.DropTable(
                name: "ProductPrices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EmployeePaymentSummaries",
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

            migrationBuilder.CreateTable(
                name: "MainProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Country = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ManufacturingCompanyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MainProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MainProducts_ManufacturingCompanies_ManufacturingCompanyId",
                        column: x => x.ManufacturingCompanyId,
                        principalTable: "ManufacturingCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MainProducts_ManufacturingCompanyId",
                table: "MainProducts",
                column: "ManufacturingCompanyId");

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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeePaymentSummary_Employees_EmployeeId",
                table: "EmployeePaymentSummary");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeTransactions_EmployeePaymentSummary_EmployeePaymentSummaryId",
                table: "EmployeeTransactions");

            migrationBuilder.DropTable(
                name: "MainProducts");

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

            migrationBuilder.AddPrimaryKey(
                name: "PK_EmployeePaymentSummaries",
                table: "EmployeePaymentSummaries",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ProductPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ManufacturingCompanyId = table.Column<int>(type: "int", nullable: false),
                    Country = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPrices_ManufacturingCompanies_ManufacturingCompanyId",
                        column: x => x.ManufacturingCompanyId,
                        principalTable: "ManufacturingCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPrices_ManufacturingCompanyId",
                table: "ProductPrices",
                column: "ManufacturingCompanyId");

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
    }
}
