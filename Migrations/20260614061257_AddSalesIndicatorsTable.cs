using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddSalesIndicatorsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesIndicators",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MainWarehouseId = table.Column<int>(type: "int", nullable: false),
                    MinimumSellingFrom = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinimumSellingTo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BasicSellingFrom = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BasicSellingTo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MiddleSellingFrom = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MiddleSellingTo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesIndicators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesIndicators_MainWarehouses_MainWarehouseId",
                        column: x => x.MainWarehouseId,
                        principalTable: "MainWarehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
     name: "IX_SalesIndicators_MainWarehouseId",
     table: "SalesIndicators",
     column: "MainWarehouseId",
     unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesIndicators");
        }
    }
}
