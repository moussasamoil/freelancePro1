using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class removediscountmessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessageCount",
                table: "SocialMediaConversations");

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "SocialMediaMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "SocialMediaMessages");

            migrationBuilder.AddColumn<int>(
                name: "MessageCount",
                table: "SocialMediaConversations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
