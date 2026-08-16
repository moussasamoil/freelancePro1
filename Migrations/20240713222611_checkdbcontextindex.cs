using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class checkdbcontextindex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Orders_Country",
                table: "Orders",
                column: "Country");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_InstantAddedDate",
                table: "Orders",
                column: "InstantAddedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_InstantAddedDate_FixedOrderDate",
                table: "Orders",
                columns: new[] { "InstantAddedDate", "FixedOrderDate" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_Country",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_InstantAddedDate",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_InstantAddedDate_FixedOrderDate",
                table: "Orders");
        }
    }
}
