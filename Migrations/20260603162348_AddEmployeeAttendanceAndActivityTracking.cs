using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    // Idempotent: nadeen's branch created these four tables on the dev DB before
    // a migration was ever authored. Prod/staging do not have them yet, so the
    // migration must still create them there. Each CREATE/INDEX is guarded by
    // IF NOT EXISTS so it's a no-op on the dev DB and a real create elsewhere.
    public partial class AddEmployeeAttendanceAndActivityTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[EmployeeActivityHourlyLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [EmployeeActivityHourlyLogs] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(max) NOT NULL,
        [EmployeeId] int NULL,
        [EmployeeName] nvarchar(max) NULL,
        [EmployeeEmail] nvarchar(max) NULL,
        [EmployeeImageUrl] nvarchar(max) NULL,
        [ActivityDate] date NOT NULL,
        [HourStartAt] datetime2 NOT NULL,
        [HourEndAt] datetime2 NOT NULL,
        [FirstSeenAt] datetime2 NULL,
        [LastSeenAt] datetime2 NULL,
        [LastActivityAt] datetime2 NULL,
        [TotalOnlineSeconds] int NOT NULL,
        [TotalActiveSeconds] int NOT NULL,
        [CurrentPage] nvarchar(max) NULL,
        [IsTabActive] bit NOT NULL,
        [IpAddress] nvarchar(max) NULL,
        [UserAgent] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_EmployeeActivityHourlyLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeActivityHourlyLogs_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id])
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[EmployeeActivityLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [EmployeeActivityLogs] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(max) NOT NULL,
        [EmployeeId] int NULL,
        [EmployeeName] nvarchar(max) NULL,
        [EmployeeEmail] nvarchar(max) NULL,
        [EmployeeImageUrl] nvarchar(max) NULL,
        [ActivityDate] date NOT NULL,
        [FirstSeenAt] datetime2 NOT NULL,
        [LastSeenAt] datetime2 NOT NULL,
        [LastActivityAt] datetime2 NULL,
        [CurrentPage] nvarchar(max) NULL,
        [IsTabActive] bit NOT NULL,
        [TotalOnlineSeconds] int NOT NULL,
        [TotalActiveSeconds] int NOT NULL,
        [LastHeartbeatAt] datetime2 NULL,
        [IpAddress] nvarchar(max) NULL,
        [UserAgent] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_EmployeeActivityLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeActivityLogs_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id])
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[EmployeeAttendanceLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [EmployeeAttendanceLogs] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(max) NOT NULL,
        [EmployeeId] int NULL,
        [EmployeeEmail] nvarchar(max) NULL,
        [EmployeeName] nvarchar(max) NULL,
        [CheckInAt] datetime2 NOT NULL,
        [CheckOutAt] datetime2 NULL,
        [FaceImagePath] nvarchar(max) NULL,
        [CheckOutFaceImagePath] nvarchar(max) NULL,
        [CheckInIpAddress] nvarchar(max) NULL,
        [CheckInLocation] nvarchar(max) NULL,
        [CheckOutIpAddress] nvarchar(max) NULL,
        [CheckOutLocation] nvarchar(max) NULL,
        [SalaryAtCheckIn] decimal(18,2) NULL,
        [DeductionAmount] decimal(18,2) NULL,
        [DeductionReason] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_EmployeeAttendanceLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeAttendanceLogs_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id])
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[EmployeeWorkShifts]', N'U') IS NULL
BEGIN
    CREATE TABLE [EmployeeWorkShifts] (
        [Id] int NOT NULL IDENTITY,
        [EmployeeId] int NOT NULL,
        [ShiftStartTime] time NOT NULL,
        [ShiftEndTime] time NOT NULL,
        [AllowedIpAddress] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_EmployeeWorkShifts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeWorkShifts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
    );
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmployeeActivityHourlyLogs_EmployeeId' AND object_id = OBJECT_ID(N'[EmployeeActivityHourlyLogs]'))
    CREATE INDEX [IX_EmployeeActivityHourlyLogs_EmployeeId] ON [EmployeeActivityHourlyLogs] ([EmployeeId]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmployeeActivityLogs_EmployeeId' AND object_id = OBJECT_ID(N'[EmployeeActivityLogs]'))
    CREATE INDEX [IX_EmployeeActivityLogs_EmployeeId] ON [EmployeeActivityLogs] ([EmployeeId]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmployeeAttendanceLogs_EmployeeId' AND object_id = OBJECT_ID(N'[EmployeeAttendanceLogs]'))
    CREATE INDEX [IX_EmployeeAttendanceLogs_EmployeeId] ON [EmployeeAttendanceLogs] ([EmployeeId]);
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmployeeWorkShifts_EmployeeId' AND object_id = OBJECT_ID(N'[EmployeeWorkShifts]'))
    CREATE INDEX [IX_EmployeeWorkShifts_EmployeeId] ON [EmployeeWorkShifts] ([EmployeeId]);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID(N'[EmployeeActivityHourlyLogs]', N'U') IS NOT NULL DROP TABLE [EmployeeActivityHourlyLogs];");
            migrationBuilder.Sql("IF OBJECT_ID(N'[EmployeeActivityLogs]', N'U') IS NOT NULL DROP TABLE [EmployeeActivityLogs];");
            migrationBuilder.Sql("IF OBJECT_ID(N'[EmployeeAttendanceLogs]', N'U') IS NOT NULL DROP TABLE [EmployeeAttendanceLogs];");
            migrationBuilder.Sql("IF OBJECT_ID(N'[EmployeeWorkShifts]', N'U') IS NOT NULL DROP TABLE [EmployeeWorkShifts];");
        }
    }
}
