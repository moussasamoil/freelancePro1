using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddDelegateEmployeeIdToOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DelegateEmployeeId",
                table: "Orders",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DelegateEmployeeId",
                table: "Orders",
                column: "DelegateEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_AspNetUsers_DelegateEmployeeId",
                table: "Orders",
                column: "DelegateEmployeeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_AspNetUsers_DelegateEmployeeId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DelegateEmployeeId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DelegateEmployeeId",
                table: "Orders");
        }
    }
}
