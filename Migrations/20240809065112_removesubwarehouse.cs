using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class removesubwarehouse : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_SubWarehouses_SubwWarehousesId",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "IX_Warehouses_SubwWarehousesId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "SubwWarehouse",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "SubwWarehousesId",
                table: "Warehouses");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubwWarehouse",
                table: "Warehouses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubwWarehousesId",
                table: "Warehouses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_SubwWarehousesId",
                table: "Warehouses",
                column: "SubwWarehousesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_SubWarehouses_SubwWarehousesId",
                table: "Warehouses",
                column: "SubwWarehousesId",
                principalTable: "SubWarehouses",
                principalColumn: "Id");
        }
    }
}
