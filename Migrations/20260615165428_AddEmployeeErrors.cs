using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddEmployeeErrors : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeErrors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ErrorText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(700)", maxLength: 700, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedByUserName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedByUserName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeletedByUserName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeErrors_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeErrorEditHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeErrorId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    OldErrorText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewErrorText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OldImageUrl = table.Column<string>(type: "nvarchar(700)", maxLength: 700, nullable: true),
                    NewImageUrl = table.Column<string>(type: "nvarchar(700)", maxLength: 700, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EditedByUserName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeErrorEditHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeErrorEditHistories_EmployeeErrors_EmployeeErrorId",
                        column: x => x.EmployeeErrorId,
                        principalTable: "EmployeeErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeErrorEditHistories_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeErrorEditHistories_CreatedAt",
                table: "EmployeeErrorEditHistories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeErrorEditHistories_EmployeeErrorId",
                table: "EmployeeErrorEditHistories",
                column: "EmployeeErrorId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeErrorEditHistories_EmployeeId",
                table: "EmployeeErrorEditHistories",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeErrors_CreatedAt",
                table: "EmployeeErrors",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeErrors_EmployeeId",
                table: "EmployeeErrors",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeErrors_IsDeleted",
                table: "EmployeeErrors",
                column: "IsDeleted");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeErrorEditHistories");

            migrationBuilder.DropTable(
                name: "EmployeeErrors");
        }
    }
}
