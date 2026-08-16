using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class socialmediaupdates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "SocialMediaMessages");

            migrationBuilder.RenameColumn(
                name: "IsHidden",
                table: "SocialMediaConversations",
                newName: "IsRead");

            migrationBuilder.AddColumn<bool>(
                name: "Gender",
                table: "SocialMediaConversations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOrder",
                table: "SocialMediaConversations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PageName",
                table: "SocialMediaConversations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SocialMediaType",
                table: "SocialMediaConversations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gender",
                table: "SocialMediaConversations");

            migrationBuilder.DropColumn(
                name: "IsOrder",
                table: "SocialMediaConversations");

            migrationBuilder.DropColumn(
                name: "PageName",
                table: "SocialMediaConversations");

            migrationBuilder.DropColumn(
                name: "SocialMediaType",
                table: "SocialMediaConversations");

            migrationBuilder.RenameColumn(
                name: "IsRead",
                table: "SocialMediaConversations",
                newName: "IsHidden");

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "SocialMediaMessages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
