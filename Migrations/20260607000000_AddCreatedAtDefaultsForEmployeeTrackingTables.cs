using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    // Hardening migration. The four employee tracking tables
    // (EmployeeWorkShifts, EmployeeAttendanceLogs, EmployeeActivityLogs,
    // EmployeeActivityHourlyLogs) declare CreatedAt as NOT NULL but the
    // CREATE TABLE in 20260603162348_AddEmployeeAttendanceAndActivityTracking
    // omitted a SQL DEFAULT. EF always supplies an explicit value via the
    // model's `= DateTime.Now` initializer, but raw INSERTs that omit the
    // column crash (see Login.EnsureEmployeeVisibleInLoginListAsync regression).
    //
    // This adds a SYSDATETIME() default to each so any future raw INSERT
    // self-heals. EF behavior is unchanged (it still sends an explicit value).
    //
    // Guard rationale: we check sys.columns.default_object_id, NOT the
    // constraint name. The previous attempt keyed on `DF_<Table>_CreatedAt`,
    // which missed defaults bound under SQL Server's auto-generated names
    // (e.g. DF__EmployeeW__Creat__abc12345) created manually outside of
    // migrations. Keying on the column's binding catches both named and
    // anonymous defaults, so the migration is a no-op on hosts where a
    // default is already present under any name.
    public partial class AddCreatedAtDefaultsForEmployeeTrackingTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[EmployeeWorkShifts]')
      AND name = N'CreatedAt'
      AND default_object_id <> 0
)
    ALTER TABLE [EmployeeWorkShifts]
        ADD CONSTRAINT [DF_EmployeeWorkShifts_CreatedAt] DEFAULT SYSDATETIME() FOR [CreatedAt];
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[EmployeeAttendanceLogs]')
      AND name = N'CreatedAt'
      AND default_object_id <> 0
)
    ALTER TABLE [EmployeeAttendanceLogs]
        ADD CONSTRAINT [DF_EmployeeAttendanceLogs_CreatedAt] DEFAULT SYSDATETIME() FOR [CreatedAt];
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[EmployeeActivityLogs]')
      AND name = N'CreatedAt'
      AND default_object_id <> 0
)
    ALTER TABLE [EmployeeActivityLogs]
        ADD CONSTRAINT [DF_EmployeeActivityLogs_CreatedAt] DEFAULT SYSDATETIME() FOR [CreatedAt];
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[EmployeeActivityHourlyLogs]')
      AND name = N'CreatedAt'
      AND default_object_id <> 0
)
    ALTER TABLE [EmployeeActivityHourlyLogs]
        ADD CONSTRAINT [DF_EmployeeActivityHourlyLogs_CreatedAt] DEFAULT SYSDATETIME() FOR [CreatedAt];
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only drop the constraints this migration created (by name). Any
            // pre-existing anonymous defaults left by manual schema edits are
            // not ours to remove on rollback.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_EmployeeWorkShifts_CreatedAt')
    ALTER TABLE [EmployeeWorkShifts] DROP CONSTRAINT [DF_EmployeeWorkShifts_CreatedAt];
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_EmployeeAttendanceLogs_CreatedAt')
    ALTER TABLE [EmployeeAttendanceLogs] DROP CONSTRAINT [DF_EmployeeAttendanceLogs_CreatedAt];
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_EmployeeActivityLogs_CreatedAt')
    ALTER TABLE [EmployeeActivityLogs] DROP CONSTRAINT [DF_EmployeeActivityLogs_CreatedAt];
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_EmployeeActivityHourlyLogs_CreatedAt')
    ALTER TABLE [EmployeeActivityHourlyLogs] DROP CONSTRAINT [DF_EmployeeActivityHourlyLogs_CreatedAt];
");
        }
    }
}
