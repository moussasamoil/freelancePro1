using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddOrderStatusUpdateSelections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderStatusUpdateSelections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    TargetStatus = table.Column<int>(type: "int", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SelectedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SelectedByName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SelectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusUpdateSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStatusUpdateSelections_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusUpdateSelections_OrderId",
                table: "OrderStatusUpdateSelections",
                column: "OrderId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusUpdateSelections_IsActive_TargetStatus",
                table: "OrderStatusUpdateSelections",
                columns: new[] { "IsActive", "TargetStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusUpdateSelections_ExpiresAt",
                table: "OrderStatusUpdateSelections",
                column: "ExpiresAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderStatusUpdateSelections");
        }
    }
}
