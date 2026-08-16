using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddEmployeeBonusSystem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BonusPaymentId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeBonusPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DatePaid = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProExtraAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalOrderCount = table.Column<int>(type: "int", nullable: false),
                    ProOrderCount = table.Column<int>(type: "int", nullable: false),
                    SuccessOrderCount = table.Column<int>(type: "int", nullable: false),
                    ProcessingOrderCount = table.Column<int>(type: "int", nullable: false),
                    ProcessingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SuccessAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeBonusPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeBonusPayments_AspNetUsers_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeBonusRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BonusPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BonusProcessingPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProBonusPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProBonusProcessingPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeBonusRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeBonusRates_AspNetUsers_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BonusPaymentId",
                table: "Orders",
                column: "BonusPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBonusPayments_EmployeeId",
                table: "EmployeeBonusPayments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBonusRates_EmployeeId",
                table: "EmployeeBonusRates",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_EmployeeBonusPayments_BonusPaymentId",
                table: "Orders",
                column: "BonusPaymentId",
                principalTable: "EmployeeBonusPayments",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_EmployeeBonusPayments_BonusPaymentId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "EmployeeBonusPayments");

            migrationBuilder.DropTable(
                name: "EmployeeBonusRates");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BonusPaymentId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BonusPaymentId",
                table: "Orders");
        }
    }
}
