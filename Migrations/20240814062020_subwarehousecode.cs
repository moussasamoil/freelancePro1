using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class subwarehousecode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductCode",
                table: "Warehouses");

            migrationBuilder.AddColumn<string>(
                name: "ProductCode",
                table: "SubWarehouses",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductCode",
                table: "SubWarehouses");

            migrationBuilder.AddColumn<string>(
                name: "ProductCode",
                table: "Warehouses",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
