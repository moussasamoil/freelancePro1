using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddFailureReasonImageUrlToOrderStatusHistories : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
    name: "FailureReasonImageUrl",
    table: "OrderStatusHistories",
    type: "nvarchar(700)",
    maxLength: 700,
    nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureReasonImageUrl",
                table: "OrderStatusHistories");
        }
    }
}
