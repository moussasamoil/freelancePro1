using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddFieldsToOrderEditHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CampaignId",
                table: "OrderEditHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Chaturl",
                table: "OrderEditHistories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryPrice",
                table: "OrderEditHistories",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_OrderEditHistories_CampaignId",
                table: "OrderEditHistories",
                column: "CampaignId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderEditHistories_Campaigns_CampaignId",
                table: "OrderEditHistories",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderEditHistories_Campaigns_CampaignId",
                table: "OrderEditHistories");

            migrationBuilder.DropIndex(
                name: "IX_OrderEditHistories_CampaignId",
                table: "OrderEditHistories");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "OrderEditHistories");

            migrationBuilder.DropColumn(
                name: "Chaturl",
                table: "OrderEditHistories");

            migrationBuilder.DropColumn(
                name: "DeliveryPrice",
                table: "OrderEditHistories");
        }
    }
}
