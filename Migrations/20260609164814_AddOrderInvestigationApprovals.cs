using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddOrderInvestigationApprovals : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderInvestigationApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),

                    OrderId = table.Column<int>(type: "int", nullable: false),

                    ApplicationUserId = table.Column<string>(
                        type: "nvarchar(450)",
                        maxLength: 450,
                        nullable: false),

                    EmployeeName = table.Column<string>(
                        type: "nvarchar(200)",
                        maxLength: 200,
                        nullable: false),

                    ApprovedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false,
                        defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderInvestigationApprovals", x => x.Id);

                    table.ForeignKey(
                        name: "FK_OrderInvestigationApprovals_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderInvestigationApprovals_OrderId",
                table: "OrderInvestigationApprovals",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderInvestigationApprovals_ApplicationUserId",
                table: "OrderInvestigationApprovals",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderInvestigationApprovals_ApprovedAt",
                table: "OrderInvestigationApprovals",
                column: "ApprovedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderInvestigationApprovals");
        }
    }
}