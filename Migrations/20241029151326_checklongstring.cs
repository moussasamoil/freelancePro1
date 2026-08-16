using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class checklongstring : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the SocialMediaMessages table (as it has a foreign key dependency).
            migrationBuilder.DropTable(name: "SocialMediaMessages");

            // Drop the SocialMediaConversations table.
            migrationBuilder.DropTable(name: "SocialMediaConversations");

            // Recreate the SocialMediaConversations table with updated schema.
            migrationBuilder.CreateTable(
                name: "SocialMediaConversations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ConversationId = table.Column<string>(nullable: true),
                    UserId = table.Column<string>(nullable: true),
                    UserName = table.Column<string>(nullable: true),
                    PageId = table.Column<string>(nullable: true),
                    PageName = table.Column<string>(nullable: true),
                    Gender = table.Column<bool>(nullable: false),
                    IsArchived = table.Column<bool>(nullable: false),
                    IsOrder = table.Column<bool>(nullable: false),
                    IsRead = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SocialMediaType = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialMediaConversations", x => x.Id);
                }
            );

            // Recreate the SocialMediaMessages table with the foreign key.
            migrationBuilder.CreateTable(
                name: "SocialMediaMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SocialMediaConversationId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MessageId = table.Column<string>(nullable: true),
                    SenderId = table.Column<string>(nullable: true),
                    Text = table.Column<string>(nullable: true),
                    IsFromUser = table.Column<bool>(nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialMediaMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialMediaMessages_SocialMediaConversations_SocialMediaConversationId",
                        column: x => x.SocialMediaConversationId,
                        principalTable: "SocialMediaConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                }
            );

            // Add an index on the foreign key column for performance (optional).
            migrationBuilder.CreateIndex(
                name: "IX_SocialMediaMessages_SocialMediaConversationId",
                table: "SocialMediaMessages",
                column: "SocialMediaConversationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the SocialMediaMessages table first due to the foreign key constraint.
            migrationBuilder.DropTable(name: "SocialMediaMessages");

            // Drop the SocialMediaConversations table.
            migrationBuilder.DropTable(name: "SocialMediaConversations");
        }
    }
}
