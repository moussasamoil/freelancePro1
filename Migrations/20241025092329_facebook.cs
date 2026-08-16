using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class facebook : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastMessageTimestamp",
                table: "SocialMediaConversations",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "LastMessage",
                table: "SocialMediaConversations",
                newName: "UserId");

            migrationBuilder.AddColumn<bool>(
                name: "IsFromUser",
                table: "SocialMediaMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MessageId",
                table: "SocialMediaMessages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderId",
                table: "SocialMediaMessages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConversationId",
                table: "SocialMediaConversations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "SocialMediaConversations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "SocialMediaConversations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFromUser",
                table: "SocialMediaMessages");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "SocialMediaMessages");

            migrationBuilder.DropColumn(
                name: "SenderId",
                table: "SocialMediaMessages");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "SocialMediaConversations");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "SocialMediaConversations");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "SocialMediaConversations");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "SocialMediaConversations",
                newName: "LastMessage");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "SocialMediaConversations",
                newName: "LastMessageTimestamp");
        }
    }
}
