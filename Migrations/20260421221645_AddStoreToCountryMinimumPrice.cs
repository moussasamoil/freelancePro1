using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddStoreToCountryMinimumPrice : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ManufacturingCompanyId",
                table: "CountryMinimumPrices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CountryMinimumPrices_ManufacturingCompanyId",
                table: "CountryMinimumPrices",
                column: "ManufacturingCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_CountryMinimumPrices_ManufacturingCompanies_ManufacturingCompanyId",
                table: "CountryMinimumPrices",
                column: "ManufacturingCompanyId",
                principalTable: "ManufacturingCompanies",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CountryMinimumPrices_ManufacturingCompanies_ManufacturingCompanyId",
                table: "CountryMinimumPrices");

            migrationBuilder.DropIndex(
                name: "IX_CountryMinimumPrices_ManufacturingCompanyId",
                table: "CountryMinimumPrices");

            migrationBuilder.DropColumn(
                name: "ManufacturingCompanyId",
                table: "CountryMinimumPrices");
        }
    }
}
