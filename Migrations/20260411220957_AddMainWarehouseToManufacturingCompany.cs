using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddMainWarehouseToManufacturingCompany : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MainWarehouseId",
                table: "ManufacturingCompanies",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingCompanies_MainWarehouseId",
                table: "ManufacturingCompanies",
                column: "MainWarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_ManufacturingCompanies_MainWarehouses_MainWarehouseId",
                table: "ManufacturingCompanies",
                column: "MainWarehouseId",
                principalTable: "MainWarehouses",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ManufacturingCompanies_MainWarehouses_MainWarehouseId",
                table: "ManufacturingCompanies");

            migrationBuilder.DropIndex(
                name: "IX_ManufacturingCompanies_MainWarehouseId",
                table: "ManufacturingCompanies");

            migrationBuilder.DropColumn(
                name: "MainWarehouseId",
                table: "ManufacturingCompanies");
        }
    }
}
