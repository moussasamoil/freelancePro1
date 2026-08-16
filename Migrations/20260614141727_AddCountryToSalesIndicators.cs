using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddCountryToSalesIndicators : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Country only. Do not alter CreatedByUserId / UpdatedByUserId.
            migrationBuilder.AddColumn<int>(
                name: "Country",
                table: "SalesIndicators",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // If an old unique index exists on MainWarehouseId only, remove it
            // because now duplicate products are allowed for different countries.
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_SalesIndicators_MainWarehouseId'
      AND object_id = OBJECT_ID(N'[dbo].[SalesIndicators]')
)
BEGIN
    DROP INDEX [IX_SalesIndicators_MainWarehouseId] ON [dbo].[SalesIndicators];
END
");

            // Create a unique index for Country + MainWarehouseId.
            // This means the same product can be added once per country only.
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_SalesIndicators_Country_MainWarehouseId'
      AND object_id = OBJECT_ID(N'[dbo].[SalesIndicators]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_SalesIndicators_Country_MainWarehouseId]
    ON [dbo].[SalesIndicators] ([Country], [MainWarehouseId]);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_SalesIndicators_Country_MainWarehouseId'
      AND object_id = OBJECT_ID(N'[dbo].[SalesIndicators]')
)
BEGIN
    DROP INDEX [IX_SalesIndicators_Country_MainWarehouseId] ON [dbo].[SalesIndicators];
END
");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "SalesIndicators");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_SalesIndicators_MainWarehouseId'
      AND object_id = OBJECT_ID(N'[dbo].[SalesIndicators]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_SalesIndicators_MainWarehouseId]
    ON [dbo].[SalesIndicators] ([MainWarehouseId]);
END
");
        }
    }
}
