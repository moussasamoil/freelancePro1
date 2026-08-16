using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class subwarehouseid : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubWarehouseId",
                table: "Warehouses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_SubWarehouseId",
                table: "Warehouses",
                column: "SubWarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_SubWarehouses_SubWarehouseId",
                table: "Warehouses",
                column: "SubWarehouseId",
                principalTable: "SubWarehouses",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_SubWarehouses_SubWarehouseId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_SubWarehouseId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "SubWarehouseId",
                table: "Warehouses");
        }
    }
}
