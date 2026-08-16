using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class warehouseidmaininsubwarehouse : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MainWarehouseId",
                table: "SubWarehouses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubWarehouses_MainWarehouseId",
                table: "SubWarehouses",
                column: "MainWarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_SubWarehouses_MainWarehouses_MainWarehouseId",
                table: "SubWarehouses",
                column: "MainWarehouseId",
                principalTable: "MainWarehouses",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubWarehouses_MainWarehouses_MainWarehouseId",
                table: "SubWarehouses");

            migrationBuilder.DropIndex(
                name: "IX_SubWarehouses_MainWarehouseId",
                table: "SubWarehouses");

            migrationBuilder.DropColumn(
                name: "MainWarehouseId",
                table: "SubWarehouses");
        }
    }
}
