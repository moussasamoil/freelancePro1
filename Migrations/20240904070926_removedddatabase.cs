using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class removedddatabase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductShipmentWarehouses");

            migrationBuilder.DropTable(
                name: "ProductShipmentInvoices");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductShipmentInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeliveryCompanyId = table.Column<int>(type: "int", nullable: false),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomId = table.Column<int>(type: "int", nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductShipmentInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductShipmentInvoices_DeliveryCompanies_DeliveryCompanyId",
                        column: x => x.DeliveryCompanyId,
                        principalTable: "DeliveryCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductShipmentWarehouses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductShipmentInvoiceId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductShipmentWarehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductShipmentWarehouses_ProductShipmentInvoices_ProductShipmentInvoiceId",
                        column: x => x.ProductShipmentInvoiceId,
                        principalTable: "ProductShipmentInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductShipmentWarehouses_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductShipmentInvoices_DeliveryCompanyId",
                table: "ProductShipmentInvoices",
                column: "DeliveryCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductShipmentWarehouses_ProductShipmentInvoiceId",
                table: "ProductShipmentWarehouses",
                column: "ProductShipmentInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductShipmentWarehouses_WarehouseId",
                table: "ProductShipmentWarehouses",
                column: "WarehouseId");
        }
    }
}
