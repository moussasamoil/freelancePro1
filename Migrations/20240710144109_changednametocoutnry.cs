using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class changednametocoutnry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Currency",
                table: "ExchangeRates",
                newName: "Country");

            migrationBuilder.AlterColumn<string>(
                name: "ProductCode",
                table: "Warehouses",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Country",
                table: "ExchangeRates",
                newName: "Currency");

            migrationBuilder.AlterColumn<string>(
                name: "ProductCode",
                table: "Warehouses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
