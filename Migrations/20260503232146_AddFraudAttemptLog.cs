using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class AddFraudAttemptLog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FraudAttemptLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderTelephoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderSecondTelephoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MatchedField = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatchedDigits = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExistingOrderId = table.Column<int>(type: "int", nullable: true),
                    ManufacturingCompanyId = table.Column<int>(type: "int", nullable: true),
                    AttemptedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedCustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedSourceName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedChatUrl = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FraudAttemptLogs", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FraudAttemptLogs");
        }
    }
}
