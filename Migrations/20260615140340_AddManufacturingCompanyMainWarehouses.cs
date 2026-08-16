using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddManufacturingCompanyMainWarehouses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManufacturingCompanyMainWarehouses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ManufacturingCompanyId = table.Column<int>(type: "int", nullable: false),
                    MainWarehouseId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManufacturingCompanyMainWarehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManufacturingCompanyMainWarehouses_MainWarehouses_MainWarehouseId",
                        column: x => x.MainWarehouseId,
                        principalTable: "MainWarehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ManufacturingCompanyMainWarehouses_ManufacturingCompanies_ManufacturingCompanyId",
                        column: x => x.ManufacturingCompanyId,
                        principalTable: "ManufacturingCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingCompanyMainWarehouses_Company_Warehouse",
                table: "ManufacturingCompanyMainWarehouses",
                columns: new[] { "ManufacturingCompanyId", "MainWarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingCompanyMainWarehouses_MainWarehouseId",
                table: "ManufacturingCompanyMainWarehouses",
                column: "MainWarehouseId");

            // ترحيل بيانات المتاجر القديمة:
            // أي متجر كان له MainWarehouseId واحد، يتم تسجيله في جدول الربط الجديد.
            migrationBuilder.Sql(@"
INSERT INTO ManufacturingCompanyMainWarehouses (ManufacturingCompanyId, MainWarehouseId)
SELECT Id, MainWarehouseId
FROM ManufacturingCompanies
WHERE MainWarehouseId IS NOT NULL
  AND MainWarehouseId > 0
  AND NOT EXISTS (
      SELECT 1
      FROM ManufacturingCompanyMainWarehouses x
      WHERE x.ManufacturingCompanyId = ManufacturingCompanies.Id
        AND x.MainWarehouseId = ManufacturingCompanies.MainWarehouseId
  );
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManufacturingCompanyMainWarehouses");
        }
    }
}
