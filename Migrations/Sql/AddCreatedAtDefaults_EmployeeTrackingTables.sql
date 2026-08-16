-- One-shot hardening: add SQL DEFAULT(SYSDATETIME()) to the CreatedAt column on
-- the four employee tracking tables (EmployeeWorkShifts, EmployeeAttendanceLogs,
-- EmployeeActivityLogs, EmployeeActivityHourlyLogs).
--
-- Why:
--   The columns are NOT NULL but were authored without a SQL DEFAULT, so any raw
--   INSERT that omits CreatedAt (e.g. Login.cshtml.cs/EnsureEmployeeVisibleInLogin..)
--   crashes with "Cannot insert the value NULL into column 'CreatedAt'".
--   The C# call site has been patched, but a SQL default closes the class of bug
--   for any future raw INSERT and matches the model's `= DateTime.Now` initializer.
--
-- Behavior notes:
--   - Idempotent: each ADD is guarded by sys.default_constraints lookup, so re-runs
--     are no-ops.
--   - SYSDATETIME() returns datetime2, matching the column type. (GETDATE() returns
--     datetime and would coerce.)
--   - Does NOT backfill: no rows need fixing — the failing INSERTs were rolled back.
--   - Does NOT change EF behavior: EF always sends an explicit CreatedAt, the
--     default only fires when an INSERT omits the column.
--
-- HOW TO RUN:
--   This script intentionally opens a transaction and does NOT commit. Run it,
--   inspect the verification SELECT at the bottom, then:
--       COMMIT TRAN;     -- to apply
--       ROLLBACK TRAN;   -- to abort

SET XACT_ABORT ON;
BEGIN TRAN;

-- EmployeeWorkShifts.CreatedAt
IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints
    WHERE name = 'DF_EmployeeWorkShifts_CreatedAt'
)
    ALTER TABLE dbo.EmployeeWorkShifts
    ADD CONSTRAINT DF_EmployeeWorkShifts_CreatedAt
        DEFAULT SYSDATETIME() FOR CreatedAt;

-- EmployeeAttendanceLogs.CreatedAt
IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints
    WHERE name = 'DF_EmployeeAttendanceLogs_CreatedAt'
)
    ALTER TABLE dbo.EmployeeAttendanceLogs
    ADD CONSTRAINT DF_EmployeeAttendanceLogs_CreatedAt
        DEFAULT SYSDATETIME() FOR CreatedAt;

-- EmployeeActivityLogs.CreatedAt
IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints
    WHERE name = 'DF_EmployeeActivityLogs_CreatedAt'
)
    ALTER TABLE dbo.EmployeeActivityLogs
    ADD CONSTRAINT DF_EmployeeActivityLogs_CreatedAt
        DEFAULT SYSDATETIME() FOR CreatedAt;

-- EmployeeActivityHourlyLogs.CreatedAt
IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints
    WHERE name = 'DF_EmployeeActivityHourlyLogs_CreatedAt'
)
    ALTER TABLE dbo.EmployeeActivityHourlyLogs
    ADD CONSTRAINT DF_EmployeeActivityHourlyLogs_CreatedAt
        DEFAULT SYSDATETIME() FOR CreatedAt;

-- Verification: all four constraints should appear after the ALTERs.
SELECT
    OBJECT_NAME(parent_object_id) AS TableName,
    COL_NAME(parent_object_id, parent_column_id) AS ColumnName,
    name AS ConstraintName,
    definition AS DefaultExpression
FROM sys.default_constraints
WHERE name IN (
    'DF_EmployeeWorkShifts_CreatedAt',
    'DF_EmployeeAttendanceLogs_CreatedAt',
    'DF_EmployeeActivityLogs_CreatedAt',
    'DF_EmployeeActivityHourlyLogs_CreatedAt'
);

-- Transaction left OPEN intentionally. Review the result set above, then:
--   COMMIT TRAN;     -- apply
--   ROLLBACK TRAN;   -- abort
