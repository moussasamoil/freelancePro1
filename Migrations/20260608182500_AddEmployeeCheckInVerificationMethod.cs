using lotus_blue.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260608182500_AddEmployeeCheckInVerificationMethod")]
    public partial class AddEmployeeCheckInVerificationMethod : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Employees', 'CheckInVerificationMethod') IS NULL
                BEGIN
                    ALTER TABLE [Employees]
                    ADD [CheckInVerificationMethod] nvarchar(20) NOT NULL
                    CONSTRAINT [DF_Employees_CheckInVerificationMethod] DEFAULT N'Photo'
                END
            ");

            migrationBuilder.Sql(@"
                UPDATE [Employees]
                SET [CheckInVerificationMethod] = N'Photo'
                WHERE [CheckInVerificationMethod] IS NULL
                   OR LTRIM(RTRIM([CheckInVerificationMethod])) = N''
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Employees', 'CheckInVerificationMethod') IS NOT NULL
                BEGIN
                    DECLARE @ConstraintName nvarchar(200);

                    SELECT @ConstraintName = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c
                        ON c.default_object_id = dc.object_id
                    INNER JOIN sys.tables t
                        ON t.object_id = c.object_id
                    WHERE t.name = N'Employees'
                      AND c.name = N'CheckInVerificationMethod';

                    IF @ConstraintName IS NOT NULL
                    BEGIN
                        EXEC(N'ALTER TABLE [Employees] DROP CONSTRAINT [' + @ConstraintName + N']');
                    END

                    ALTER TABLE [Employees] DROP COLUMN [CheckInVerificationMethod];
                END
            ");
        }
    }
}
