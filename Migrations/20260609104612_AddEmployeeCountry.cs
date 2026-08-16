using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddEmployeeCountry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'Country') IS NULL
BEGIN
    ALTER TABLE dbo.Employees
    ADD Country nvarchar(50) NULL;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'Country') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Employees
    DROP COLUMN Country;
END
");
        }
    }
}