using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddOrderMetaActionClicks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[OrderMetaActionClicks]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrderMetaActionClicks]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [OrderId] INT NOT NULL,
        [UserId] NVARCHAR(450) NULL,
        [EmployeeName] NVARCHAR(300) NULL,
        [Reason] NVARCHAR(100) NOT NULL,
        [OtherText] NVARCHAR(500) NULL,
        [MetaUrl] NVARCHAR(1000) NULL,
        [ClickedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_OrderMetaActionClicks_ClickedAt] DEFAULT SYSUTCDATETIME(),

        CONSTRAINT [PK_OrderMetaActionClicks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_OrderMetaActionClicks_OrderId_ClickedAt'
      AND object_id = OBJECT_ID(N'[dbo].[OrderMetaActionClicks]')
)
BEGIN
    CREATE INDEX [IX_OrderMetaActionClicks_OrderId_ClickedAt]
    ON [dbo].[OrderMetaActionClicks] ([OrderId], [ClickedAt] DESC);
END;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[OrderMetaActionClicks]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[OrderMetaActionClicks];
END;
");
        }
    }
}