using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class ispecialorder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsClientSpecial",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsClientSpecial",
                table: "Orders");
        }
    }
}
