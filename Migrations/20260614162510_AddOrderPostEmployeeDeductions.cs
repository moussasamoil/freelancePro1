using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddOrderPostEmployeeDeductions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[OrderPostDeletedHistories]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrderPostDeletedHistories]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [OrderPostId] INT NOT NULL,
        [OrderId] INT NOT NULL,
        [Type] INT NOT NULL,
        [Body] NVARCHAR(MAX) NULL,
        [AuthorUserId] NVARCHAR(450) NULL,
        [AuthorName] NVARCHAR(256) NULL,
        [CreatedAt] DATETIME2 NULL,
        [DeletedAt] DATETIME2 NOT NULL,
        [DeletedByUserId] NVARCHAR(450) NULL,
        [DeletedByName] NVARCHAR(256) NULL,
        CONSTRAINT [PK_OrderPostDeletedHistories] PRIMARY KEY ([Id])
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[OrderPostDeletedHistories]', N'U') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_OrderPostDeletedHistories_OrderPostId'
      AND object_id = OBJECT_ID(N'[dbo].[OrderPostDeletedHistories]')
)
BEGIN
    CREATE UNIQUE INDEX [UX_OrderPostDeletedHistories_OrderPostId]
    ON [dbo].[OrderPostDeletedHistories] ([OrderPostId]);
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[OrderPostDeletedHistories]', N'U') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_OrderPostDeletedHistories_OrderId_Type_DeletedAt'
      AND object_id = OBJECT_ID(N'[dbo].[OrderPostDeletedHistories]')
)
BEGIN
    CREATE INDEX [IX_OrderPostDeletedHistories_OrderId_Type_DeletedAt]
    ON [dbo].[OrderPostDeletedHistories] ([OrderId], [Type], [DeletedAt] DESC);
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[OrderPostEmployeeDeductions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrderPostEmployeeDeductions]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [OrderId] INT NOT NULL,
        [EmployeeId] INT NOT NULL,
        [EmployeeName] NVARCHAR(256) NULL,
        [Amount] DECIMAL(18,2) NOT NULL,
        [OrderTotal] DECIMAL(18,2) NOT NULL,
        [Reason] NVARCHAR(MAX) NULL,
        [ProblemText] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [CreatedByUserId] NVARCHAR(450) NULL,
        [CreatedByName] NVARCHAR(256) NULL,
        [EmployeeTransactionId] INT NULL,
        CONSTRAINT [PK_OrderPostEmployeeDeductions] PRIMARY KEY ([Id])
    );
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[OrderPostEmployeeDeductions]', N'U') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_OrderPostEmployeeDeductions_OrderId_CreatedAt'
      AND object_id = OBJECT_ID(N'[dbo].[OrderPostEmployeeDeductions]')
)
BEGIN
    CREATE INDEX [IX_OrderPostEmployeeDeductions_OrderId_CreatedAt]
    ON [dbo].[OrderPostEmployeeDeductions] ([OrderId], [CreatedAt] DESC);
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[OrderPostEmployeeDeductions]', N'U') IS NOT NULL
AND NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_OrderPostEmployeeDeductions_EmployeeTransactionId'
      AND object_id = OBJECT_ID(N'[dbo].[OrderPostEmployeeDeductions]')
)
BEGIN
    CREATE INDEX [IX_OrderPostEmployeeDeductions_EmployeeTransactionId]
    ON [dbo].[OrderPostEmployeeDeductions] ([EmployeeTransactionId]);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[OrderPostEmployeeDeductions]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[OrderPostEmployeeDeductions];
END
");
        }
    }
}
