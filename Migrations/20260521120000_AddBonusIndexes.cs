using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    // Promotes the EmployeeBonusRate.EmployeeId index to UNIQUE (one rate row per
    // employee) and replaces the single-column EmployeeBonusPayment.EmployeeId
    // index with a composite (EmployeeId, DatePaid) index — used by the archive
    // page and panel-freeze queries that filter by employee then order by DatePaid.
    //
    // Up() is idempotent: an earlier migration (TightenEmployeeBonusIndexes) was
    // committed that performs the same schema change, so on databases where that
    // already ran this migration must safely no-op and still register itself in
    // __EFMigrationsHistory. Each step is guarded by sys.indexes checks.
    public partial class AddBonusIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_EmployeeBonusRates_EmployeeId'
      AND object_id = OBJECT_ID('[EmployeeBonusRates]')
      AND is_unique = 0
)
BEGIN
    DROP INDEX [IX_EmployeeBonusRates_EmployeeId] ON [EmployeeBonusRates];
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_EmployeeBonusRates_EmployeeId'
      AND object_id = OBJECT_ID('[EmployeeBonusRates]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_EmployeeBonusRates_EmployeeId]
        ON [EmployeeBonusRates] ([EmployeeId]);
END;

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_EmployeeBonusPayments_EmployeeId'
      AND object_id = OBJECT_ID('[EmployeeBonusPayments]')
)
BEGIN
    DROP INDEX [IX_EmployeeBonusPayments_EmployeeId] ON [EmployeeBonusPayments];
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_EmployeeBonusPayments_EmployeeId_DatePaid'
      AND object_id = OBJECT_ID('[EmployeeBonusPayments]')
)
BEGIN
    CREATE INDEX [IX_EmployeeBonusPayments_EmployeeId_DatePaid]
        ON [EmployeeBonusPayments] ([EmployeeId], [DatePaid]);
END;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmployeeBonusPayments_EmployeeId_DatePaid",
                table: "EmployeeBonusPayments");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBonusPayments_EmployeeId",
                table: "EmployeeBonusPayments",
                column: "EmployeeId");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeBonusRates_EmployeeId",
                table: "EmployeeBonusRates");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBonusRates_EmployeeId",
                table: "EmployeeBonusRates",
                column: "EmployeeId");
        }
    }
}
