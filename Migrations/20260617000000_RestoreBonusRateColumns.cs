using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class RestoreBonusRateColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BonusPercentage",
                table: "EmployeeBonusRates",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BonusProcessingPercentage",
                table: "EmployeeBonusRates",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProBonusProcessingPercentage",
                table: "EmployeeBonusRates",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BonusPercentage",
                table: "EmployeeBonusRates");

            migrationBuilder.DropColumn(
                name: "BonusProcessingPercentage",
                table: "EmployeeBonusRates");

            migrationBuilder.DropColumn(
                name: "ProBonusProcessingPercentage",
                table: "EmployeeBonusRates");
        }
    }
}
