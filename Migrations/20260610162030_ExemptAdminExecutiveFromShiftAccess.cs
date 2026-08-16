using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class ExemptAdminExecutiveFromShiftAccess : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE e
                SET e.ApplyShiftAccess = 0
                FROM Employees e
                INNER JOIN AspNetUsers u ON u.Id = e.ApplicationUserId
                INNER JOIN AspNetUserRoles ur ON ur.UserId = u.Id
                INNER JOIN AspNetRoles r ON r.Id = ur.RoleId
                WHERE r.Name IN ('Admin', 'ExecutiveDirector');
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE e
                SET e.ApplyShiftAccess = 1
                FROM Employees e
                INNER JOIN AspNetUsers u ON u.Id = e.ApplicationUserId
                INNER JOIN AspNetUserRoles ur ON ur.UserId = u.Id
                INNER JOIN AspNetRoles r ON r.Id = ur.RoleId
                WHERE r.Name IN ('Admin', 'ExecutiveDirector');
            ");
        }
    }
}