using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddManufacturingCompanyToCampaign : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ManufacturingCompanyId",
                table: "Campaigns",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_ManufacturingCompanyId",
                table: "Campaigns",
                column: "ManufacturingCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Campaigns_ManufacturingCompanies_ManufacturingCompanyId",
                table: "Campaigns",
                column: "ManufacturingCompanyId",
                principalTable: "ManufacturingCompanies",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Campaigns_ManufacturingCompanies_ManufacturingCompanyId",
                table: "Campaigns");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_ManufacturingCompanyId",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "ManufacturingCompanyId",
                table: "Campaigns");
        }
    }
}
